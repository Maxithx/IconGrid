using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherTabsState : INotifyPropertyChanged
    {
        private string _selectedTab;

        public LauncherTabsState(IEnumerable<string> tabNames, string selectedTab)
        {
            Tabs = new ObservableCollection<string>(tabNames);
            _selectedTab = selectedTab;

            if (Tabs.Any() && !Tabs.Contains(_selectedTab))
            {
                _selectedTab = Tabs[0];
            }
        }

        public ObservableCollection<string> Tabs { get; }

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetField(ref _selectedTab, value))
                {
                    if (string.IsNullOrWhiteSpace(_selectedTab) && Tabs.Any())
                    {
                        _selectedTab = Tabs[0];
                        OnPropertyChanged(nameof(SelectedTab));
                    }
                }
            }
        }

        public void AddTab(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (!Tabs.Contains(name))
            {
                Tabs.Add(name);
            }

            SelectedTab = name;
        }

        public bool RenameTab(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return false;

            var index = Tabs.IndexOf(oldName);
            if (index < 0)
                return false;

            if (!Tabs.Contains(newName))
            {
                Tabs[index] = newName;
            }

            if (SelectedTab == oldName)
            {
                SelectedTab = newName;
            }

            return true;
        }

        public bool RemoveTab(string tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName))
                return false;

            if (!Tabs.Contains(tabName))
                return false;

            Tabs.Remove(tabName);

            if (!Tabs.Contains(SelectedTab) && Tabs.Any())
            {
                SelectedTab = Tabs[0];
            }

            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
