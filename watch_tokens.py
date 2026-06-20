import glob
import json
import os
import sys
import time


HOME = os.path.expanduser("~")
CONTINUE_TOKENS_FILE = os.path.join(
    HOME, ".continue", "dev_data", "0.2.0", "tokensGenerated.jsonl"
)
MODEL_CONFIG_ROOT = os.path.join(
    HOME, ".lmstudio", ".internal", "user-concrete-model-default-config"
)
SESSION_COUNTER_FILE = os.path.join(HOME, ".iconagrid_session_counter")

RESET = "\033[0m"
YELLOW = "\033[33m"
RED = "\033[31m"
BRIGHT_RED = "\033[91m"


def load_json(path):
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        return json.load(f)


def load_session_counter():
    """Load session counter from disk, default to 1 if file doesn't exist."""
    if os.path.exists(SESSION_COUNTER_FILE):
        try:
            with open(SESSION_COUNTER_FILE, "r", encoding="utf-8") as f:
                return int(f.read().strip())
        except Exception:
            return 1
    return 1


def save_session_counter(session_number):
    """Save session counter to disk."""
    try:
        with open(SESSION_COUNTER_FILE, "w", encoding="utf-8") as f:
            f.write(str(session_number))
    except Exception as e:
        print(f"Kunne ikke gemme sessiontaller: {e}", flush=True)


def find_model_config_path(model_name):
    if not model_name:
        return None

    normalized_path = os.path.join(MODEL_CONFIG_ROOT, *model_name.split("/")) + ".json"
    if os.path.exists(normalized_path):
        return normalized_path

    fallback_name = f"{model_name.split('/')[-1]}.json"
    pattern = os.path.join(MODEL_CONFIG_ROOT, "*", fallback_name)
    matches = glob.glob(pattern)
    return matches[0] if matches else None


def get_context_length(model_name):
    config_path = find_model_config_path(model_name)
    if not config_path:
        return None

    try:
        config = load_json(config_path)
        for field in config.get("load", {}).get("fields", []):
            if field.get("key") == "llm.load.contextLength":
                return int(field.get("value"))
    except Exception:
        return None

    return None


def read_last_jsonl_entry(path):
    if not os.path.exists(path):
        return None

    last_line = None
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            line = line.strip()
            if line:
                last_line = line

    if not last_line:
        return None

    try:
        return json.loads(last_line)
    except json.JSONDecodeError:
        return None


def make_signature(event):
    return (
        event.get("timestamp"),
        event.get("model"),
        event.get("promptTokens"),
        event.get("generatedTokens"),
    )


def print_status(event, context_length, session_number, reset_detected):
    model_name = event.get("model", "ukendt")
    prompt_tokens = int(event.get("promptTokens", 0) or 0)
    generated_tokens = int(event.get("generatedTokens", 0) or 0)
    timestamp = event.get("timestamp", "")

    remaining = max(context_length - prompt_tokens, 0)
    used_percent = (prompt_tokens / context_length) * 100 if context_length else 0

    color = ""
    warning = ""
    if used_percent >= 95:
        color = BRIGHT_RED
        warning = " KRITISK"
    elif used_percent >= 90:
        color = RED
        warning = " HOJ"
    elif used_percent >= 80:
        color = YELLOW
        warning = " PAS PAA"

    session_label = f"Session siden start: {session_number}"
    if reset_detected:
        session_label += " NY"

    line = (
        f"Tid: {timestamp} | Model: {model_name} | Seneste request prompt: {prompt_tokens}/{context_length} "
        f"| Resterende context: {remaining} | Prompt-udnyttelse: {used_percent:.1f}% "
        f"| {session_label} | Seneste request output: {generated_tokens}{warning}"
    )

    padded_line = line.ljust(160)
    sys.stdout.write(f"\r{color}{padded_line}{RESET}")
    sys.stdout.flush()


def main():
    print("Overvager Continue tokens for aktiv LM Studio-model...", flush=True)
    print(f"Laeser fra: {CONTINUE_TOKENS_FILE}", flush=True)
    print("Viser tokens for seneste request, ikke LM Studio's interne runtime-hukommelse.", flush=True)
    print("Sessiontaeller huskes mellem opstartsessioner. Tryk Ctrl+C for at stoppe.", flush=True)
    print("-" * 72, flush=True)

    last_signature = None
    last_prompt_tokens = None
    session_number = load_session_counter()
    print(f"Starter med sessiontaeller: {session_number}", flush=True)

    while True:
        try:
            event = read_last_jsonl_entry(CONTINUE_TOKENS_FILE)
            if not event:
                print("Ingen data fundet endnu i tokensGenerated.jsonl.", flush=True)
                time.sleep(2)
                continue

            model_name = event.get("model")
            context_length = get_context_length(model_name)
            signature = make_signature(event)

            if signature != last_signature:
                prompt_tokens = int(event.get("promptTokens", 0) or 0)
                reset_detected = False

                if last_prompt_tokens is not None and prompt_tokens < last_prompt_tokens:
                    session_number += 1
                    save_session_counter(session_number)
                    reset_detected = True

                if context_length is None:
                    print(f"Kunne ikke finde contextLength for model: {model_name}", flush=True)
                else:
                    print_status(event, context_length, session_number, reset_detected)
                last_signature = signature
                last_prompt_tokens = prompt_tokens

            time.sleep(1)
        except KeyboardInterrupt:
            print("\nLukker watcher.", flush=True)
            break
        except Exception as exc:
            print(f"Fejl under laesning: {exc}", flush=True)
            time.sleep(2)


if __name__ == "__main__":
    main()
