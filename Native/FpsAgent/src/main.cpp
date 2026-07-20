#include <windows.h>
#include <evntrace.h>
#include <evntcons.h>
#include <psapi.h>
#include <tlhelp32.h>

#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <deque>
#include <fstream>
#include <locale>
#include <mutex>
#include <optional>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

namespace
{
    constexpr wchar_t kIgnoredProcessName[] = L"IconGrid.exe";
    constexpr wchar_t kSharedMemoryName[] = L"Local\\IconGrid.NativeFps.Live";
    constexpr char kSessionName[] = "IconGridFpsAgent_ETW";
    constexpr unsigned long long kDxgKrnlKeywordPresent = 0x8000000;
    constexpr unsigned long long kDxgKrnlKeywordBase = 0x1;
    constexpr USHORT kDxgiPresentStartEventId = 42;
    constexpr USHORT kD3D9PresentStartEventId = 1;
    constexpr USHORT kDxgKrnlPresentInfoEventId = 0x00B8;
    constexpr USHORT kDxgKrnlFlipInfoEventId = 0x00A8;
    constexpr USHORT kDxgKrnlBlitInfoEventId = 0x00A6;
    constexpr DWORD kTargetPollIntervalMs = 500;
    constexpr DWORD kStateWriteIntervalMs = 2;
    constexpr double kRollingFpsWindowSeconds = 0.025;
    constexpr double kInstantFrameFloorSeconds = 1.0 / 360.0;
    constexpr double kInstantFrameCeilingSeconds = 1.0 / 8.0;

    const GUID kDxgiProvider = {0xCA11C036, 0x0102, 0x4A2D, {0xA6, 0xAD, 0xF0, 0x3C, 0xFE, 0xD5, 0xD3, 0xC9}};
    const GUID kD3D9Provider = {0x783ACA0A, 0x790E, 0x4D7F, {0x84, 0x51, 0xAA, 0x85, 0x05, 0x11, 0xC6, 0xB9}};
    const GUID kDxgKrnlProvider = {0x802EC45A, 0x1E99, 0x4B83, {0x99, 0x20, 0x87, 0xC9, 0x82, 0x77, 0xBA, 0x9D}};

    struct FpsState
    {
        std::wstring fpsStatus = L"--";
        std::wstring fpsSource = L"NativeFpsAgent";
        double fpsValue = 0.0;
        DWORD parentPid = 0;
        DWORD rootPid = 0;
        DWORD targetPid = 0;
        std::wstring targetProcessName;
        std::wstring lockedExecutableName;
        std::wstring lockedExecutablePath;
        DWORD candidatePid = 0;
        std::wstring candidateProcessName;
        std::wstring workerStartedAtUtc;
        std::wstring lastTargetLockAtUtc;
        std::wstring lastEtwAttemptAtUtc;
        int etwStartAttemptCount = 0;
        int etwStartFailureCount = 0;
        std::wstring lastEtwError;
        bool dxgKrnlEnabled = false;
        bool dxgiEnabled = false;
        bool d3d9Enabled = false;
        std::wstring lastDxgKrnlError;
        std::wstring lastDxgiError;
        std::wstring lastD3d9Error;
        bool isElevated = false;
        bool etwRunning = false;
        bool etwEventsReceived = false;
        int preFilterDxgiEventCount = 0;
        int preFilterD3d9EventCount = 0;
        int preFilterDxgKrnlEventCount = 0;
        int matchedDxgiEventCount = 0;
        int matchedD3d9EventCount = 0;
        int matchedDxgKrnlEventCount = 0;
        int dxgiEventCount = 0;
        int d3d9EventCount = 0;
        int dxgKrnlEventCount = 0;
        std::wstring debugMessage;
        std::wstring error;
    };

    struct Args
    {
        std::wstring statePath;
        std::optional<DWORD> parentPid;
        std::wstring targetExeName;
        std::wstring targetPath;
        std::wstring workingDir;
        std::optional<DWORD> rootPid;
        unsigned long long rootStartFileTimeUtc = 0;
        unsigned long long launchFileTimeUtc = 0;
    };

#pragma pack(push, 1)
    struct SharedFpsState
    {
        long long sequenceStart = 0;
        unsigned int magic = 0x49474650; // "IGFP"
        unsigned int version = 1;
        long long capturedTicksUtc = 0;
        double fpsValue = 0.0;
        unsigned int targetPid = 0;
        unsigned int flags = 0;
        unsigned int reserved1 = 0;
        unsigned int reserved2 = 0;
        long long sequenceEnd = 0;
    };
#pragma pack(pop)

    struct ProcessCandidate
    {
        DWORD pid = 0;
        DWORD parentPid = 0;
        std::wstring processName;
        std::wstring processPath;
        unsigned long long startFileTimeUtc = 0;
        int score = 0;
    };

    std::mutex g_stateMutex;
    FpsState g_state;
    std::atomic<DWORD> g_targetPid = 0;
    std::atomic<bool> g_running = true;
    std::atomic<bool> g_etwRunning = false;
    TRACEHANDLE g_etwSession = 0;
    TRACEHANDLE g_etwTrace = 0;
    std::thread g_etwThread;
    HANDLE g_sharedMemoryHandle = nullptr;
    SharedFpsState* g_sharedMemoryView = nullptr;
    std::atomic<long long> g_sharedSequence = 0;
    double g_qpcFrequency = 0.0;
    std::wstring g_targetExeName;
    std::wstring g_targetPath;
    std::wstring g_workingDir;
    std::optional<DWORD> g_rootPid;
    unsigned long long g_rootStartFileTimeUtc = 0;
    unsigned long long g_launchFileTimeUtc = 0;
    bool g_isElevated = false;
    DWORD g_parentPid = 0;
    std::atomic<bool> g_etwEventsReceived = false;
    std::atomic<int> g_preFilterDxgiEventCount = 0;
    std::atomic<int> g_preFilterD3d9EventCount = 0;
    std::atomic<int> g_preFilterDxgKrnlEventCount = 0;
    std::atomic<int> g_dxgiEventCount = 0;
    std::atomic<int> g_d3d9EventCount = 0;
    std::atomic<int> g_dxgKrnlEventCount = 0;
    constexpr DWORD kEtwRetryIntervalMs = 3000;
    constexpr unsigned int kSharedFlagHasFps = 0x1;
    constexpr unsigned int kSharedFlagEtwRunning = 0x2;

    std::wstring GetIsoUtcNow()
    {
        SYSTEMTIME systemTime{};
        GetSystemTime(&systemTime);
        wchar_t buffer[48];
        swprintf_s(
            buffer,
            L"%04u-%02u-%02uT%02u:%02u:%02u.%03uZ",
            systemTime.wYear,
            systemTime.wMonth,
            systemTime.wDay,
            systemTime.wHour,
            systemTime.wMinute,
            systemTime.wSecond,
            systemTime.wMilliseconds);
        return buffer;
    }

    long long GetUtcTicksNow()
    {
        FILETIME fileTime{};
        GetSystemTimeAsFileTime(&fileTime);
        ULARGE_INTEGER value{};
        value.LowPart = fileTime.dwLowDateTime;
        value.HighPart = fileTime.dwHighDateTime;
        constexpr long long ticksBetween1601And0001 = 504911232000000000LL;
        return static_cast<long long>(value.QuadPart) + ticksBetween1601And0001;
    }

    std::wstring EscapeJson(const std::wstring& value)
    {
        std::wstring escaped;
        escaped.reserve(value.size() + 8);

        for (const auto ch : value)
        {
            switch (ch)
            {
            case L'\\': escaped += L"\\\\"; break;
            case L'"': escaped += L"\\\""; break;
            case L'\r': escaped += L"\\r"; break;
            case L'\n': escaped += L"\\n"; break;
            case L'\t': escaped += L"\\t"; break;
            default:
                if (ch < 0x20)
                {
                    wchar_t unicodeEscape[7];
                    swprintf_s(unicodeEscape, L"\\u%04x", static_cast<unsigned int>(ch));
                    escaped += unicodeEscape;
                }
                else
                {
                    escaped.push_back(ch);
                }
                break;
            }
        }

        return escaped;
    }

    std::wstring ToLower(std::wstring value)
    {
        for (auto& ch : value)
        {
            ch = static_cast<wchar_t>(towlower(ch));
        }

        return value;
    }

    void SetError(const std::wstring& error)
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.error = error;
        g_state.lastEtwError = error;
    }

    void SetProviderEnableState(bool dxgKrnlEnabled, bool dxgiEnabled, bool d3d9Enabled)
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.dxgKrnlEnabled = dxgKrnlEnabled;
        g_state.dxgiEnabled = dxgiEnabled;
        g_state.d3d9Enabled = d3d9Enabled;
    }

    void SetProviderError(const wchar_t* provider, const std::wstring& error)
    {
        std::scoped_lock lock(g_stateMutex);
        if (wcscmp(provider, L"DxgKrnl") == 0)
        {
            g_state.lastDxgKrnlError = error;
        }
        else if (wcscmp(provider, L"DXGI") == 0)
        {
            g_state.lastDxgiError = error;
        }
        else if (wcscmp(provider, L"D3D9") == 0)
        {
            g_state.lastD3d9Error = error;
        }
    }

    void SetDebugMessage(const std::wstring& message)
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.debugMessage = message;
    }

    void WriteSharedFpsState(double fpsValue, bool hasFps)
    {
        if (g_sharedMemoryView == nullptr)
        {
            return;
        }

        SharedFpsState snapshot{};
        snapshot.magic = 0x49474650;
        snapshot.version = 1;
        snapshot.capturedTicksUtc = GetUtcTicksNow();
        snapshot.fpsValue = hasFps ? fpsValue : 0.0;
        snapshot.targetPid = g_targetPid.load(std::memory_order_relaxed);
        snapshot.flags = g_etwRunning.load(std::memory_order_relaxed) ? kSharedFlagEtwRunning : 0;
        if (hasFps)
        {
            snapshot.flags |= kSharedFlagHasFps;
        }

        const auto sequence = g_sharedSequence.fetch_add(2, std::memory_order_relaxed) + 2;
        snapshot.sequenceStart = sequence;
        snapshot.sequenceEnd = sequence;

        std::atomic_thread_fence(std::memory_order_release);
        *g_sharedMemoryView = snapshot;
        std::atomic_thread_fence(std::memory_order_release);
    }

    bool InitializeSharedMemory()
    {
        g_sharedMemoryHandle = CreateFileMappingW(
            INVALID_HANDLE_VALUE,
            nullptr,
            PAGE_READWRITE,
            0,
            sizeof(SharedFpsState),
            kSharedMemoryName);
        if (g_sharedMemoryHandle == nullptr)
        {
            return false;
        }

        g_sharedMemoryView = static_cast<SharedFpsState*>(MapViewOfFile(
            g_sharedMemoryHandle,
            FILE_MAP_READ | FILE_MAP_WRITE,
            0,
            0,
            sizeof(SharedFpsState)));
        if (g_sharedMemoryView == nullptr)
        {
            CloseHandle(g_sharedMemoryHandle);
            g_sharedMemoryHandle = nullptr;
            return false;
        }

        ZeroMemory(g_sharedMemoryView, sizeof(SharedFpsState));
        WriteSharedFpsState(0.0, false);
        return true;
    }

    void CleanupSharedMemory()
    {
        if (g_sharedMemoryView != nullptr)
        {
            UnmapViewOfFile(g_sharedMemoryView);
            g_sharedMemoryView = nullptr;
        }

        if (g_sharedMemoryHandle != nullptr)
        {
            CloseHandle(g_sharedMemoryHandle);
            g_sharedMemoryHandle = nullptr;
        }
    }

    std::wstring FormatEtwError(const wchar_t* operation, unsigned long errorCode)
    {
        std::wostringstream message;
        message << operation << L" failed with Win32 error " << errorCode;
        return message.str();
    }

    void UpdateDebugCounts()
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.fpsSource = L"NativeFpsAgent";
        g_state.parentPid = g_parentPid;
        g_state.rootPid = g_rootPid.value_or(0);
        g_state.isElevated = g_isElevated;
        g_state.etwRunning = g_etwRunning.load(std::memory_order_relaxed);
        g_state.etwEventsReceived = g_etwEventsReceived.load(std::memory_order_relaxed);
        g_state.preFilterDxgiEventCount = g_preFilterDxgiEventCount.load(std::memory_order_relaxed);
        g_state.preFilterD3d9EventCount = g_preFilterD3d9EventCount.load(std::memory_order_relaxed);
        g_state.preFilterDxgKrnlEventCount = g_preFilterDxgKrnlEventCount.load(std::memory_order_relaxed);
        g_state.matchedDxgiEventCount = g_dxgiEventCount.load(std::memory_order_relaxed);
        g_state.matchedD3d9EventCount = g_d3d9EventCount.load(std::memory_order_relaxed);
        g_state.matchedDxgKrnlEventCount = g_dxgKrnlEventCount.load(std::memory_order_relaxed);
        g_state.dxgiEventCount = g_dxgiEventCount.load(std::memory_order_relaxed);
        g_state.d3d9EventCount = g_d3d9EventCount.load(std::memory_order_relaxed);
        g_state.dxgKrnlEventCount = g_dxgKrnlEventCount.load(std::memory_order_relaxed);
    }

    void UpdateTargetState(DWORD pid, const std::wstring& processName)
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.targetPid = pid;
        g_state.targetProcessName = processName;
        g_state.lockedExecutableName = g_targetExeName;
        g_state.lockedExecutablePath = g_targetPath;
        if (pid == 0)
        {
            g_state.fpsStatus = L"--";
            g_state.fpsValue = 0.0;
            WriteSharedFpsState(0.0, false);
        }
        else
        {
            g_state.lastTargetLockAtUtc = GetIsoUtcNow();
        }
    }

    void UpdateCandidateState(DWORD pid, const std::wstring& processName)
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.candidatePid = pid;
        g_state.candidateProcessName = processName;
    }

    void UpdateFpsState(const std::wstring& fpsStatus, double fpsValue = 0.0)
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.fpsStatus = fpsStatus;
        g_state.fpsSource = L"NativeFpsAgent";
        g_state.fpsValue = fpsValue;
        if (fpsStatus != L"--")
        {
            g_state.error.clear();
        }

        WriteSharedFpsState(fpsValue, fpsStatus != L"--" && fpsValue > 0.0);
    }

    void RecordEtwAttempt()
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.lastEtwAttemptAtUtc = GetIsoUtcNow();
        g_state.etwStartAttemptCount++;
    }

    void RecordEtwFailure()
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.etwStartFailureCount++;
    }

    bool IsCurrentProcessElevated()
    {
        HANDLE token = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            return false;
        }

        TOKEN_ELEVATION elevation{};
        DWORD returnedLength = 0;
        const auto success = GetTokenInformation(
            token,
            TokenElevation,
            &elevation,
            sizeof(elevation),
            &returnedLength) != FALSE;

        CloseHandle(token);
        return success && elevation.TokenIsElevated != 0;
    }

    bool ParentIsAlive(std::optional<DWORD> parentPid)
    {
        if (!parentPid.has_value())
        {
            return true;
        }

        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, parentPid.value());
        if (process == nullptr)
        {
            return false;
        }

        DWORD exitCode = 0;
        const auto ok = GetExitCodeProcess(process, &exitCode) != FALSE;
        CloseHandle(process);
        return ok && exitCode == STILL_ACTIVE;
    }

    bool IsProcessAlive(DWORD pid)
    {
        if (pid == 0)
        {
            return false;
        }

        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (process == nullptr)
        {
            return false;
        }

        DWORD exitCode = 0;
        const auto ok = GetExitCodeProcess(process, &exitCode) != FALSE;
        CloseHandle(process);
        return ok && exitCode == STILL_ACTIVE;
    }

    unsigned long long GetProcessStartFileTimeUtc(DWORD pid)
    {
        if (pid == 0)
        {
            return 0;
        }

        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (process == nullptr)
        {
            return 0;
        }

        FILETIME creationTime{};
        FILETIME exitTime{};
        FILETIME kernelTime{};
        FILETIME userTime{};
        unsigned long long result = 0;

        if (GetProcessTimes(process, &creationTime, &exitTime, &kernelTime, &userTime))
        {
            ULARGE_INTEGER value{};
            value.LowPart = creationTime.dwLowDateTime;
            value.HighPart = creationTime.dwHighDateTime;
            result = value.QuadPart;
        }

        CloseHandle(process);
        return result;
    }

    std::wstring GetProcessPath(DWORD pid)
    {
        if (pid == 0)
        {
            return L"";
        }

        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, FALSE, pid);
        if (process == nullptr)
        {
            return L"";
        }

        wchar_t pathBuffer[MAX_PATH];
        DWORD pathLength = MAX_PATH;
        std::wstring result;

        if (QueryFullProcessImageNameW(process, 0, pathBuffer, &pathLength))
        {
            result.assign(pathBuffer, pathLength);
        }
        else
        {
            wchar_t modulePath[MAX_PATH];
            if (GetModuleFileNameExW(process, nullptr, modulePath, MAX_PATH) > 0)
            {
                result = modulePath;
            }
        }

        CloseHandle(process);
        return result;
    }

    std::wstring GetProcessName(DWORD pid)
    {
        if (pid == 0)
        {
            return L"";
        }

        const auto fullPath = GetProcessPath(pid);
        if (fullPath.empty())
        {
            return L"";
        }

        const auto separator = fullPath.find_last_of(L"\\/");
        return separator == std::wstring::npos ? fullPath : fullPath.substr(separator + 1);
    }

    std::wstring GetDirectoryPath(const std::wstring& path)
    {
        const auto separator = path.find_last_of(L"\\/");
        return separator == std::wstring::npos ? std::wstring() : path.substr(0, separator);
    }

    bool IsSameOrChildDirectory(const std::wstring& candidateDirectory, const std::wstring& rootDirectory)
    {
        if (candidateDirectory.empty() || rootDirectory.empty())
        {
            return false;
        }

        const auto normalizedCandidate = ToLower(candidateDirectory);
        const auto normalizedRoot = ToLower(rootDirectory);
        if (normalizedCandidate == normalizedRoot)
        {
            return true;
        }

        if (normalizedCandidate.size() <= normalizedRoot.size())
        {
            return false;
        }

        if (normalizedCandidate.compare(0, normalizedRoot.size(), normalizedRoot) != 0)
        {
            return false;
        }

        const auto separator = normalizedCandidate[normalizedRoot.size()];
        return separator == L'\\' || separator == L'/';
    }

    bool IsRelatedToRootProcess(DWORD pid, const std::vector<ProcessCandidate>& processes)
    {
        if (!g_rootPid.has_value() || pid == 0)
        {
            return false;
        }

        DWORD currentPid = pid;
        for (int depth = 0; depth < 8 && currentPid != 0; ++depth)
        {
            if (currentPid == g_rootPid.value())
            {
                return true;
            }

            DWORD parentPid = 0;
            for (const auto& process : processes)
            {
                if (process.pid == currentPid)
                {
                    parentPid = process.parentPid;
                    break;
                }
            }

            if (parentPid == 0 || parentPid == currentPid)
            {
                break;
            }

            currentPid = parentPid;
        }

        return false;
    }

    std::optional<ProcessCandidate> FindTargetProcess()
    {
        if (g_targetExeName.empty() && g_targetPath.empty() && g_workingDir.empty() && !g_rootPid.has_value())
        {
            SetDebugMessage(L"No configured FPS target session metadata.");
            return std::nullopt;
        }

        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            SetDebugMessage(L"CreateToolhelp32Snapshot failed.");
            return std::nullopt;
        }

        PROCESSENTRY32W entry{};
        entry.dwSize = sizeof(entry);
        std::vector<ProcessCandidate> processes;
        const auto targetName = ToLower(g_targetExeName);
        const auto targetPath = ToLower(g_targetPath);
        const auto workingDirectory = ToLower(g_workingDir);
        const auto targetDirectory = ToLower(GetDirectoryPath(g_targetPath));

        if (Process32FirstW(snapshot, &entry))
        {
            do
            {
                if (entry.th32ProcessID == 0 || entry.th32ProcessID == GetCurrentProcessId())
                {
                    continue;
                }

                const std::wstring processName = entry.szExeFile;
                if (!_wcsicmp(processName.c_str(), kIgnoredProcessName))
                {
                    continue;
                }

                ProcessCandidate candidate;
                candidate.pid = entry.th32ProcessID;
                candidate.parentPid = entry.th32ParentProcessID;
                candidate.processName = processName;
                candidate.processPath = GetProcessPath(entry.th32ProcessID);
                candidate.startFileTimeUtc = GetProcessStartFileTimeUtc(entry.th32ProcessID);
                processes.push_back(candidate);
            } while (Process32NextW(snapshot, &entry));
        }

        CloseHandle(snapshot);

        if (g_rootPid.has_value())
        {
            for (const auto& candidate : processes)
            {
                if (candidate.pid != g_rootPid.value())
                {
                    continue;
                }

                std::wostringstream message;
                message << L"Using root PID " << candidate.pid
                        << L" directly. Name=" << candidate.processName
                        << L" Path=" << candidate.processPath;
                SetDebugMessage(message.str());
                return candidate;
            }
        }

        std::optional<ProcessCandidate> bestMatch;
        for (auto& candidate : processes)
        {
            const auto normalizedName = ToLower(candidate.processName);
            const auto normalizedPath = ToLower(candidate.processPath);
            const auto candidateDirectory = ToLower(GetDirectoryPath(candidate.processPath));
            const auto pathMatches = !targetPath.empty() && !normalizedPath.empty() && normalizedPath == targetPath;
            const auto nameMatches = !targetName.empty() && normalizedName == targetName;
            const auto workingDirectoryMatches = !workingDirectory.empty() && IsSameOrChildDirectory(candidateDirectory, workingDirectory);
            const auto targetDirectoryMatches = !targetDirectory.empty() && IsSameOrChildDirectory(candidateDirectory, targetDirectory);
            const auto relatedToRoot = IsRelatedToRootProcess(candidate.pid, processes);
            const auto isRootPid = g_rootPid.has_value() && candidate.pid == g_rootPid.value();
            const auto startMatchesRoot = g_rootStartFileTimeUtc != 0 &&
                                          candidate.startFileTimeUtc != 0 &&
                                          candidate.startFileTimeUtc >= g_rootStartFileTimeUtc;
            const auto startMatchesLaunch = g_launchFileTimeUtc != 0 &&
                                            candidate.startFileTimeUtc != 0 &&
                                            candidate.startFileTimeUtc + 10000000ULL >= g_launchFileTimeUtc;
            const auto isExactCandidate = pathMatches || nameMatches || isRootPid;

            if ((!targetName.empty() || !targetPath.empty()) && !isExactCandidate)
            {
                continue;
            }

            candidate.score = 0;
            if (pathMatches) candidate.score += 1000;
            if (nameMatches) candidate.score += 500;
            if (isRootPid) candidate.score += 450;
            if (relatedToRoot) candidate.score += 350;
            if (workingDirectoryMatches) candidate.score += 250;
            if (targetDirectoryMatches) candidate.score += 200;
            if (startMatchesRoot) candidate.score += 150;
            if (startMatchesLaunch) candidate.score += 100;
            if (candidate.startFileTimeUtc != 0 && g_launchFileTimeUtc != 0 && candidate.startFileTimeUtc < g_launchFileTimeUtc - 30000000ULL)
            {
                candidate.score -= 150;
            }

            if (candidate.score <= 0)
            {
                continue;
            }

            if (!bestMatch.has_value() ||
                candidate.score > bestMatch->score ||
                (candidate.score == bestMatch->score && candidate.startFileTimeUtc > bestMatch->startFileTimeUtc))
            {
                bestMatch = candidate;
            }
        }

        if (!bestMatch.has_value())
        {
            SetDebugMessage(L"No running process matched the current launch session.");
        }

        return bestMatch;
    }

    void WriteStateFile(const std::wstring& statePath)
    {
        FpsState stateCopy;
        {
            std::scoped_lock lock(g_stateMutex);
            stateCopy = g_state;
        }

        const std::wstring tempPath = statePath + L".tmp";

        std::wostringstream json;
        json << L"{"
             << L"\"capturedAtUtc\":\"" << EscapeJson(GetIsoUtcNow()) << L"\","
             << L"\"fpsStatus\":\"" << EscapeJson(stateCopy.fpsStatus) << L"\","
             << L"\"fpsSource\":\"" << EscapeJson(stateCopy.fpsSource) << L"\","
             << L"\"fpsValue\":" << stateCopy.fpsValue << L","
             << L"\"parentPid\":" << stateCopy.parentPid << L","
             << L"\"rootPid\":" << stateCopy.rootPid << L","
             << L"\"targetPid\":" << stateCopy.targetPid << L","
             << L"\"targetProcessName\":\"" << EscapeJson(stateCopy.targetProcessName) << L"\","
             << L"\"lockedExecutableName\":\"" << EscapeJson(stateCopy.lockedExecutableName) << L"\","
             << L"\"lockedExecutablePath\":\"" << EscapeJson(stateCopy.lockedExecutablePath) << L"\","
             << L"\"candidatePid\":" << stateCopy.candidatePid << L","
             << L"\"candidateProcessName\":\"" << EscapeJson(stateCopy.candidateProcessName) << L"\","
             << L"\"workerStartedAtUtc\":\"" << EscapeJson(stateCopy.workerStartedAtUtc) << L"\","
             << L"\"lastTargetLockAtUtc\":\"" << EscapeJson(stateCopy.lastTargetLockAtUtc) << L"\","
             << L"\"lastEtwAttemptAtUtc\":\"" << EscapeJson(stateCopy.lastEtwAttemptAtUtc) << L"\","
             << L"\"etwStartAttemptCount\":" << stateCopy.etwStartAttemptCount << L","
             << L"\"etwStartFailureCount\":" << stateCopy.etwStartFailureCount << L","
             << L"\"lastEtwError\":\"" << EscapeJson(stateCopy.lastEtwError) << L"\","
             << L"\"dxgKrnlEnabled\":" << (stateCopy.dxgKrnlEnabled ? L"true" : L"false") << L","
             << L"\"dxgiEnabled\":" << (stateCopy.dxgiEnabled ? L"true" : L"false") << L","
             << L"\"d3d9Enabled\":" << (stateCopy.d3d9Enabled ? L"true" : L"false") << L","
             << L"\"lastDxgKrnlError\":\"" << EscapeJson(stateCopy.lastDxgKrnlError) << L"\","
             << L"\"lastDxgiError\":\"" << EscapeJson(stateCopy.lastDxgiError) << L"\","
             << L"\"lastD3D9Error\":\"" << EscapeJson(stateCopy.lastD3d9Error) << L"\","
             << L"\"isElevated\":" << (stateCopy.isElevated ? L"true" : L"false") << L","
             << L"\"etwRunning\":" << (stateCopy.etwRunning ? L"true" : L"false") << L","
             << L"\"etwEventsReceived\":" << (stateCopy.etwEventsReceived ? L"true" : L"false") << L","
             << L"\"preFilterDxgiEventCount\":" << stateCopy.preFilterDxgiEventCount << L","
             << L"\"preFilterD3D9EventCount\":" << stateCopy.preFilterD3d9EventCount << L","
             << L"\"preFilterDxgKrnlEventCount\":" << stateCopy.preFilterDxgKrnlEventCount << L","
             << L"\"matchedDxgiEventCount\":" << stateCopy.matchedDxgiEventCount << L","
             << L"\"matchedD3D9EventCount\":" << stateCopy.matchedD3d9EventCount << L","
             << L"\"matchedDxgKrnlEventCount\":" << stateCopy.matchedDxgKrnlEventCount << L","
             << L"\"dxgiEventCount\":" << stateCopy.dxgiEventCount << L","
             << L"\"d3d9EventCount\":" << stateCopy.d3d9EventCount << L","
             << L"\"dxgKrnlEventCount\":" << stateCopy.dxgKrnlEventCount << L","
             << L"\"debugMessage\":\"" << EscapeJson(stateCopy.debugMessage) << L"\","
             << L"\"error\":\"" << EscapeJson(stateCopy.error) << L"\""
             << L"}";

        std::wofstream stream(tempPath, std::ios::binary | std::ios::trunc);
        stream.imbue(std::locale::classic());
        stream << json.str();
        stream.close();

        MoveFileExW(tempPath.c_str(), statePath.c_str(), MOVEFILE_REPLACE_EXISTING);
    }

    void PollLockedTarget()
    {
        DWORD lockedPid = 0;

        while (g_running.load(std::memory_order_relaxed))
        {
            if (lockedPid != 0 && IsProcessAlive(lockedPid))
            {
                const auto processName = GetProcessName(lockedPid);
                g_targetPid.store(lockedPid, std::memory_order_relaxed);
                UpdateCandidateState(lockedPid, processName);
                UpdateTargetState(lockedPid, processName);

                Sleep(kTargetPollIntervalMs);
                continue;
            }

            if (lockedPid != 0)
            {
                std::wostringstream message;
                message << L"Locked PID " << lockedPid << L" exited. Looking for a new matching process.";
                SetDebugMessage(message.str());
            }

            lockedPid = 0;
            g_targetPid.store(0, std::memory_order_relaxed);
            UpdateTargetState(0, L"");
            UpdateCandidateState(0, L"");

            const auto candidate = FindTargetProcess();
            if (candidate.has_value())
            {
                lockedPid = candidate->pid;
                g_targetPid.store(lockedPid, std::memory_order_relaxed);
                UpdateCandidateState(candidate->pid, candidate->processName);
                UpdateTargetState(candidate->pid, candidate->processName);

                std::wostringstream message;
                message << L"Locked candidate PID=" << candidate->pid
                        << L" Name=" << candidate->processName
                        << L" Path=" << candidate->processPath
                        << L" Score=" << candidate->score
                        << L" ParentPid=" << candidate->parentPid;
                SetDebugMessage(message.str());
            }

            Sleep(kTargetPollIntervalMs);
        }
    }

    void WINAPI EtwCallback(PEVENT_RECORD eventRecord)
    {
        if (!g_etwRunning.load(std::memory_order_relaxed))
        {
            return;
        }

        bool isDxgiEvent = false;
        bool isD3D9Event = false;
        bool isDxgKrnlOnlyEvent = false;

        if (memcmp(&eventRecord->EventHeader.ProviderId, &kDxgiProvider, sizeof(GUID)) == 0)
        {
            isDxgiEvent = eventRecord->EventHeader.EventDescriptor.Id == kDxgiPresentStartEventId;
        }
        else if (memcmp(&eventRecord->EventHeader.ProviderId, &kD3D9Provider, sizeof(GUID)) == 0)
        {
            isD3D9Event = eventRecord->EventHeader.EventDescriptor.Id == kD3D9PresentStartEventId;
        }
        else if (memcmp(&eventRecord->EventHeader.ProviderId, &kDxgKrnlProvider, sizeof(GUID)) == 0)
        {
            const auto eventId = eventRecord->EventHeader.EventDescriptor.Id;
            isDxgKrnlOnlyEvent = eventId == kDxgKrnlPresentInfoEventId ||
                                 eventId == kDxgKrnlFlipInfoEventId ||
                                 eventId == kDxgKrnlBlitInfoEventId;
        }

        if (!isDxgiEvent && !isD3D9Event && !isDxgKrnlOnlyEvent)
        {
            return;
        }

        g_etwEventsReceived.store(true, std::memory_order_relaxed);
        if (isDxgiEvent) ++g_preFilterDxgiEventCount;
        if (isD3D9Event) ++g_preFilterD3d9EventCount;
        if (isDxgKrnlOnlyEvent) ++g_preFilterDxgKrnlEventCount;

        const auto pid = eventRecord->EventHeader.ProcessId;
        const auto targetPid = g_targetPid.load(std::memory_order_relaxed);
        if (targetPid == 0 || pid != targetPid)
        {
            return;
        }

        const double timestampSeconds = static_cast<double>(eventRecord->EventHeader.TimeStamp.QuadPart) / g_qpcFrequency;
        static DWORD lastPid = 0;
        static std::deque<double> dxgiTimestamps;
        static std::deque<double> d3d9Timestamps;
        static std::deque<double> dxgKrnlTimestamps;
        static double lastMatchedTimestampSeconds = 0.0;

        const auto trimWindow = [](std::deque<double>& timestamps, double nowSeconds)
        {
            while (!timestamps.empty() && (nowSeconds - timestamps.front()) > kRollingFpsWindowSeconds)
            {
                timestamps.pop_front();
            }
        };

        const auto computeRollingFps = [](const std::deque<double>& timestamps) -> float
        {
            if (timestamps.size() < 2)
            {
                return 0.0f;
            }

            const auto span = timestamps.back() - timestamps.front();
            if (span <= 0.0)
            {
                return static_cast<float>(timestamps.size() / kRollingFpsWindowSeconds);
            }

            return static_cast<float>((timestamps.size() - 1) / span);
        };

        if (pid != lastPid)
        {
            lastPid = pid;
            dxgiTimestamps.clear();
            d3d9Timestamps.clear();
            dxgKrnlTimestamps.clear();
            lastMatchedTimestampSeconds = 0.0;
            g_dxgiEventCount.store(0, std::memory_order_relaxed);
            g_d3d9EventCount.store(0, std::memory_order_relaxed);
            g_dxgKrnlEventCount.store(0, std::memory_order_relaxed);
        }

        if (isDxgiEvent)
        {
            dxgiTimestamps.push_back(timestampSeconds);
        }

        if (isD3D9Event)
        {
            d3d9Timestamps.push_back(timestampSeconds);
        }

        if (isDxgKrnlOnlyEvent)
        {
            dxgKrnlTimestamps.push_back(timestampSeconds);
        }

        trimWindow(dxgiTimestamps, timestampSeconds);
        trimWindow(d3d9Timestamps, timestampSeconds);
        trimWindow(dxgKrnlTimestamps, timestampSeconds);

        const auto dxgiCount = static_cast<int>(dxgiTimestamps.size());
        const auto d3d9Count = static_cast<int>(d3d9Timestamps.size());
        const auto dxgKrnlCount = static_cast<int>(dxgKrnlTimestamps.size());
        g_dxgiEventCount.store(dxgiCount, std::memory_order_relaxed);
        g_d3d9EventCount.store(d3d9Count, std::memory_order_relaxed);
        g_dxgKrnlEventCount.store(dxgKrnlCount, std::memory_order_relaxed);

        float rollingFps = 0.0f;
        if (d3d9Count >= 2)
        {
            rollingFps = computeRollingFps(d3d9Timestamps);
        }
        else if (dxgiCount >= 2)
        {
            rollingFps = computeRollingFps(dxgiTimestamps);
        }
        else if (dxgKrnlCount >= 2)
        {
            const auto potentialFps = computeRollingFps(dxgKrnlTimestamps);
            if (potentialFps >= 20.0f)
            {
                rollingFps = potentialFps;
            }
        }

        float instantFps = 0.0f;
        if (lastMatchedTimestampSeconds > 0.0)
        {
            const auto frameSeconds = timestampSeconds - lastMatchedTimestampSeconds;
            if (frameSeconds >= kInstantFrameFloorSeconds && frameSeconds <= kInstantFrameCeilingSeconds)
            {
                instantFps = static_cast<float>(1.0 / frameSeconds);
            }
        }
        lastMatchedTimestampSeconds = timestampSeconds;

        float fps = rollingFps;
        if (instantFps > 0.0f)
        {
            if (fps <= 0.0f)
            {
                fps = instantFps;
            }
            else if (instantFps < fps)
            {
                // Make short drops hit almost immediately.
                fps = (instantFps * 0.985f) + (fps * 0.015f);
            }
            else
            {
                // Let rises follow the instant signal even more aggressively too.
                fps = (instantFps * 0.78f) + (fps * 0.22f);
            }
        }

        if (fps > 0.0f)
        {
            wchar_t fpsBuffer[16];
            swprintf_s(fpsBuffer, L"%.0f", std::round(fps));
            UpdateFpsState(fpsBuffer, fps);

            std::wostringstream message;
            message << L"ETW window PID=" << pid
                    << L" FPS=" << fpsBuffer
                    << L" DXGI=" << dxgiCount
                    << L" D3D9=" << d3d9Count
                    << L" DXGKRNL=" << dxgKrnlCount;
            SetDebugMessage(message.str());
        }
        else
        {
            UpdateFpsState(L"--", 0.0);

            std::wostringstream message;
            message << L"ETW events but no usable frame count for PID=" << pid
                    << L" DXGI=" << dxgiCount
                    << L" D3D9=" << d3d9Count
                    << L" DXGKRNL=" << dxgKrnlCount;
            SetDebugMessage(message.str());
        }

    }

    bool StartEtwSession()
    {
        LARGE_INTEGER frequency{};
        QueryPerformanceFrequency(&frequency);
        g_qpcFrequency = static_cast<double>(frequency.QuadPart);

        struct SessionBuffer
        {
            EVENT_TRACE_PROPERTIES properties;
            char name[256];
        } sessionBuffer{};

        sessionBuffer.properties.Wnode.BufferSize = sizeof(sessionBuffer);
        sessionBuffer.properties.LoggerNameOffset = offsetof(SessionBuffer, name);
        ControlTraceA(0, kSessionName, &sessionBuffer.properties, EVENT_TRACE_CONTROL_STOP);

        ZeroMemory(&sessionBuffer, sizeof(sessionBuffer));
        sessionBuffer.properties.Wnode.BufferSize = sizeof(sessionBuffer);
        sessionBuffer.properties.Wnode.Flags = WNODE_FLAG_TRACED_GUID;
        sessionBuffer.properties.Wnode.ClientContext = 1;
        sessionBuffer.properties.LogFileMode = EVENT_TRACE_REAL_TIME_MODE;
        sessionBuffer.properties.LoggerNameOffset = offsetof(SessionBuffer, name);

        auto result = StartTraceA(&g_etwSession, kSessionName, &sessionBuffer.properties);
        if (result != ERROR_SUCCESS)
        {
            SetError(FormatEtwError(L"StartTraceA", result));
            SetDebugMessage(FormatEtwError(L"StartTraceA", result));
            RecordEtwFailure();
            return false;
        }

        result = EnableTraceEx2(
            g_etwSession,
            &kDxgiProvider,
            EVENT_CONTROL_CODE_ENABLE_PROVIDER,
            TRACE_LEVEL_INFORMATION,
            0,
            0,
            0,
            nullptr);
        const auto dxgiEnabled = result == ERROR_SUCCESS;
        if (result != ERROR_SUCCESS)
        {
            const auto error = FormatEtwError(L"EnableTraceEx2(DXGI)", result);
            SetError(error);
            SetProviderError(L"DXGI", error);
            SetDebugMessage(error);
        }
        else
        {
            SetProviderError(L"DXGI", L"");
        }

        result = EnableTraceEx2(g_etwSession, &kD3D9Provider, EVENT_CONTROL_CODE_ENABLE_PROVIDER, TRACE_LEVEL_INFORMATION, 0, 0, 0, nullptr);
        const auto d3d9Enabled = result == ERROR_SUCCESS;
        if (result != ERROR_SUCCESS)
        {
            const auto error = FormatEtwError(L"EnableTraceEx2(D3D9)", result);
            SetProviderError(L"D3D9", error);
            SetDebugMessage(error);
        }
        else
        {
            SetProviderError(L"D3D9", L"");
        }

        result = EnableTraceEx2(
            g_etwSession,
            &kDxgKrnlProvider,
            EVENT_CONTROL_CODE_ENABLE_PROVIDER,
            TRACE_LEVEL_INFORMATION,
            kDxgKrnlKeywordPresent | kDxgKrnlKeywordBase,
            0,
            0,
            nullptr);
        const auto dxgKrnlEnabled = result == ERROR_SUCCESS;
        if (result != ERROR_SUCCESS)
        {
            const auto error = FormatEtwError(L"EnableTraceEx2(DxgKrnl)", result);
            SetProviderError(L"DxgKrnl", error);
            SetDebugMessage(error);
        }
        else
        {
            SetProviderError(L"DxgKrnl", L"");
        }

        SetProviderEnableState(dxgKrnlEnabled, dxgiEnabled, d3d9Enabled);

        if (!dxgiEnabled && !dxgKrnlEnabled && !d3d9Enabled)
        {
            RecordEtwFailure();
            ControlTraceA(g_etwSession, nullptr, &sessionBuffer.properties, EVENT_TRACE_CONTROL_STOP);
            g_etwSession = 0;
            return false;
        }

        EVENT_TRACE_LOGFILEA logFile{};
        logFile.LoggerName = const_cast<LPSTR>(kSessionName);
        logFile.ProcessTraceMode = PROCESS_TRACE_MODE_REAL_TIME | PROCESS_TRACE_MODE_EVENT_RECORD;
        logFile.EventRecordCallback = EtwCallback;

        g_etwTrace = OpenTraceA(&logFile);
        if (g_etwTrace == INVALID_PROCESSTRACE_HANDLE)
        {
            const auto openTraceError = GetLastError();
            SetError(FormatEtwError(L"OpenTraceA", openTraceError));
            SetDebugMessage(FormatEtwError(L"OpenTraceA", openTraceError));
            RecordEtwFailure();
            ControlTraceA(g_etwSession, nullptr, &sessionBuffer.properties, EVENT_TRACE_CONTROL_STOP);
            g_etwSession = 0;
            return false;
        }

        g_etwRunning.store(true, std::memory_order_relaxed);
        SetDebugMessage(L"ETW session started.");
        g_etwThread = std::thread([]()
        {
            TRACEHANDLE traceHandle = g_etwTrace;
            ProcessTrace(&traceHandle, 1, nullptr, nullptr);
        });

        return true;
    }

    void StopEtwSession()
    {
        if (!g_etwRunning.exchange(false, std::memory_order_relaxed))
        {
            return;
        }

        SetDebugMessage(L"Stopping ETW session.");

        if (g_etwTrace != 0 && g_etwTrace != INVALID_PROCESSTRACE_HANDLE)
        {
            CloseTrace(g_etwTrace);
            g_etwTrace = 0;
        }

        if (g_etwThread.joinable())
        {
            g_etwThread.join();
        }

        struct SessionBuffer
        {
            EVENT_TRACE_PROPERTIES properties;
            char name[256];
        } sessionBuffer{};

        sessionBuffer.properties.Wnode.BufferSize = sizeof(sessionBuffer);
        sessionBuffer.properties.LoggerNameOffset = offsetof(SessionBuffer, name);
        ControlTraceA(g_etwSession, kSessionName, &sessionBuffer.properties, EVENT_TRACE_CONTROL_STOP);
        g_etwSession = 0;
    }

    std::optional<Args> ParseArgs(int argc, wchar_t* argv[])
    {
        Args args;

        for (int index = 1; index < argc; ++index)
        {
            const std::wstring current = argv[index];
            if (current == L"--state-path" && index + 1 < argc)
            {
                args.statePath = argv[++index];
            }
            else if (current == L"--parent-pid" && index + 1 < argc)
            {
                args.parentPid = static_cast<DWORD>(_wtoi(argv[++index]));
            }
            else if (current == L"--target-exe" && index + 1 < argc)
            {
                args.targetExeName = argv[++index];
            }
            else if (current == L"--target-path" && index + 1 < argc)
            {
                args.targetPath = argv[++index];
            }
            else if (current == L"--working-dir" && index + 1 < argc)
            {
                args.workingDir = argv[++index];
            }
            else if (current == L"--root-pid" && index + 1 < argc)
            {
                args.rootPid = static_cast<DWORD>(_wtoi(argv[++index]));
            }
            else if (current == L"--root-start-filetime" && index + 1 < argc)
            {
                args.rootStartFileTimeUtc = _wcstoui64(argv[++index], nullptr, 10);
            }
            else if (current == L"--launch-filetime" && index + 1 < argc)
            {
                args.launchFileTimeUtc = _wcstoui64(argv[++index], nullptr, 10);
            }
        }

        if (args.statePath.empty())
        {
            return std::nullopt;
        }

        return args;
    }
}

int wmain(int argc, wchar_t* argv[])
{
    const auto parsedArgs = ParseArgs(argc, argv);
    if (!parsedArgs.has_value())
    {
        return 1;
    }

    g_targetExeName = parsedArgs->targetExeName;
    g_targetPath = parsedArgs->targetPath;
    g_workingDir = parsedArgs->workingDir;
    g_rootPid = parsedArgs->rootPid;
    g_parentPid = parsedArgs->parentPid.value_or(0);
    g_rootStartFileTimeUtc = parsedArgs->rootStartFileTimeUtc;
    g_launchFileTimeUtc = parsedArgs->launchFileTimeUtc;
    g_isElevated = IsCurrentProcessElevated();
    {
        std::scoped_lock lock(g_stateMutex);
        g_state.workerStartedAtUtc = GetIsoUtcNow();
        g_state.parentPid = g_parentPid;
        g_state.rootPid = g_rootPid.value_or(0);
        g_state.lockedExecutableName = g_targetExeName;
        g_state.lockedExecutablePath = g_targetPath;
    }

    InitializeSharedMemory();

    std::thread targetThread(PollLockedTarget);
    auto nextEtwRetryAt = std::chrono::steady_clock::now();
    bool reportedWaitingForTarget = false;

    while (g_running.load(std::memory_order_relaxed) && ParentIsAlive(parsedArgs->parentPid))
    {
        const auto targetPid = g_targetPid.load(std::memory_order_relaxed);
        if (!g_etwRunning.load(std::memory_order_relaxed) && targetPid == 0)
        {
            if (!reportedWaitingForTarget)
            {
                SetDebugMessage(L"Waiting for target process lock before ETW start.");
                reportedWaitingForTarget = true;
            }
        }
        else if (!g_etwRunning.load(std::memory_order_relaxed) &&
                 targetPid != 0 &&
                 std::chrono::steady_clock::now() >= nextEtwRetryAt)
        {
            RecordEtwAttempt();
            std::wostringstream retryMessage;
            retryMessage << L"Attempting ETW start for target PID=" << targetPid;
            SetDebugMessage(retryMessage.str());

            if (StartEtwSession())
            {
                SetDebugMessage(L"ETW session started after target lock.");
                SetError(L"");
            }
            reportedWaitingForTarget = false;
            nextEtwRetryAt = std::chrono::steady_clock::now() + std::chrono::milliseconds(kEtwRetryIntervalMs);
        }

        UpdateDebugCounts();
        WriteStateFile(parsedArgs->statePath);
        Sleep(kStateWriteIntervalMs);
    }

    g_running.store(false, std::memory_order_relaxed);
    if (targetThread.joinable())
    {
        targetThread.join();
    }

    StopEtwSession();
    UpdateDebugCounts();
    WriteStateFile(parsedArgs->statePath);
    CleanupSharedMemory();
    return 0;
}
