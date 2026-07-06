using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.UI;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InventoryTabsHotkey
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class InventoryTabsHotkeyPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "com.desze.invtabshotkey";
        private const string PluginName = "desze-Inventory Tabs Hotkey";
        private const string PluginVersion = "1.2.1";

        private const string TabControllerFieldName = "gclass3808_0";
        private const string TabDictionaryFieldName = "_tabDictionary";

        private static ConfigEntry<KeyboardShortcut> _nextTabKey;
        private static ConfigEntry<KeyboardShortcut> _prevTabKey;
        private static ConfigEntry<bool> _enableInMainMenu;
        private Dictionary<EInventoryTab, ConfigEntry<KeyboardShortcut>> _jumpKeys;

        private FieldInfo _tabControllerField;
        private FieldInfo _tabDictionaryField;
        private bool _reflectionFailed;
        private bool _tabDictionaryReflectionFailed;

        private void Awake()
        {
            _nextTabKey = Config.Bind(
                "Hotkeys",
                "Next Tab",
                new KeyboardShortcut(KeyCode.E),
                "Moves to the next inventory tab.");

            _prevTabKey = Config.Bind(
                "Hotkeys",
                "Previous Tab",
                new KeyboardShortcut(KeyCode.Q),
                "Moves to the previous inventory tab.");

            _enableInMainMenu = Config.Bind(
                "General",
                "Enable In Main Menu",
                true,
                "If disabled, the mod won't do anything while you're in the main menu " +
                "(it will still work normally in raid).");

            _jumpKeys = new Dictionary<EInventoryTab, ConfigEntry<KeyboardShortcut>>
            {
                [EInventoryTab.Overall] = Config.Bind(
                    "Jump To Tab", "Overall Tab", new KeyboardShortcut(KeyCode.C),
                    "Jumps directly to the Overall tab."),
                [EInventoryTab.Gear] = Config.Bind(
                    "Jump To Tab", "Gear Tab", new KeyboardShortcut(KeyCode.G),
                    "Jumps directly to the Gear tab."),
                [EInventoryTab.Health] = Config.Bind(
                    "Jump To Tab", "Health Tab", new KeyboardShortcut(KeyCode.R),
                    "Jumps directly to the Health tab."),
                [EInventoryTab.Skills] = Config.Bind(
                    "Jump To Tab", "Skills Tab", new KeyboardShortcut(KeyCode.J),
                    "Jumps directly to the Skills tab."),
                [EInventoryTab.Map] = Config.Bind(
                    "Jump To Tab", "Map Tab", new KeyboardShortcut(KeyCode.M),
                    "Jumps directly to the Map tab."),
                [EInventoryTab.Notes] = Config.Bind(
                    "Jump To Tab", "Tasks Tab", new KeyboardShortcut(KeyCode.T),
                    "Jumps directly to the Tasks tab."),
                [EInventoryTab.Achievements] = Config.Bind(
                    "Jump To Tab", "Achievements Tab", new KeyboardShortcut(KeyCode.K),
                    "Jumps directly to the Achievements tab."),
                [EInventoryTab.Prestige] = Config.Bind(
                    "Jump To Tab", "Prestige Tab", new KeyboardShortcut(KeyCode.L),
                    "Jumps directly to the Prestige tab."),
            };

            Logger.LogInfo("[InventoryTabsHotkey] Successfully Loaded.");
        }

        private void Update()
        {
            if (_reflectionFailed)
            {
                return;
            }

            if (!Singleton<GameWorld>.Instantiated && !_enableInMainMenu.Value)
            {
                return;
            }

            if (_nextTabKey.Value.IsDown())
            {
                ShiftTab(1);
                return;
            }

            if (_prevTabKey.Value.IsDown())
            {
                ShiftTab(-1);
                return;
            }

            foreach (KeyValuePair<EInventoryTab, ConfigEntry<KeyboardShortcut>> jumpKey in _jumpKeys)
            {
                if (jumpKey.Value.Value.IsDown())
                {
                    JumpToTab(jumpKey.Key);
                    return;
                }
            }
        }

        private void ShiftTab(int direction)
        {
            if (IsTypingInTextField() || IsHoldingLeftMouseButton())
            {
                return;
            }

            InventoryScreen inventoryScreen = Singleton<CommonUI>.Instance?.InventoryScreen;
            if (inventoryScreen == null || !inventoryScreen.isActiveAndEnabled)
            {
                return;
            }

            GClass3808 tabController = GetTabController(inventoryScreen);
            if (tabController == null)
            {
                return;
            }

            Tab[] allTabs = tabController.Tab_0;
            if (allTabs == null || allTabs.Length == 0)
            {
                return;
            }

            int currentIndex = tabController.SelectedTabIndex;
            if (currentIndex < 0)
            {
                return;
            }

            int nextIndex = ((currentIndex + direction) % allTabs.Length + allTabs.Length) % allTabs.Length;
            Tab nextTab = allTabs[nextIndex];

            tabController.TryHide();
            tabController.Show(nextTab, true);
        }

        private void JumpToTab(EInventoryTab tab)
        {
            if (IsTypingInTextField() || IsHoldingLeftMouseButton())
            {
                return;
            }

            InventoryScreen inventoryScreen = Singleton<CommonUI>.Instance?.InventoryScreen;
            if (inventoryScreen == null || !inventoryScreen.isActiveAndEnabled)
            {
                return;
            }

            GClass3808 tabController = GetTabController(inventoryScreen);
            if (tabController == null)
            {
                return;
            }

            IReadOnlyDictionary<EInventoryTab, Tab> tabDictionary = GetTabDictionary(inventoryScreen);
            if (tabDictionary == null || !tabDictionary.TryGetValue(tab, out Tab targetTab) || targetTab == null)
            {
                return;
            }

            tabController.TryHide();
            tabController.Show(targetTab, true);
        }

        private GClass3808 GetTabController(InventoryScreen inventoryScreen)
        {
            if (_tabControllerField == null)
            {
                _tabControllerField = AccessTools.Field(typeof(InventoryScreen), TabControllerFieldName);
                if (_tabControllerField == null)
                {
                    Logger.LogError($"[InventoryTabsHotkey] Could not find field '{TabControllerFieldName}' on InventoryScreen.");
                    _reflectionFailed = true;
                    return null;
                }
            }

            return _tabControllerField.GetValue(inventoryScreen) as GClass3808;
        }

        private IReadOnlyDictionary<EInventoryTab, Tab> GetTabDictionary(InventoryScreen inventoryScreen)
        {
            if (_tabDictionaryReflectionFailed)
            {
                return null;
            }

            if (_tabDictionaryField == null)
            {
                _tabDictionaryField = AccessTools.Field(typeof(InventoryScreen), TabDictionaryFieldName);
                if (_tabDictionaryField == null)
                {
                    Logger.LogError($"[InventoryTabsHotkey] Could not find field '{TabDictionaryFieldName}' on InventoryScreen.");
                    _tabDictionaryReflectionFailed = true;
                    return null;
                }
            }

            return _tabDictionaryField.GetValue(inventoryScreen) as IReadOnlyDictionary<EInventoryTab, Tab>;
        }

        private bool IsTypingInTextField()
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && selected.GetComponent<TMPro.TMP_InputField>() != null;
        }

        private bool IsHoldingLeftMouseButton()
        {
            return Input.GetMouseButton(0);
        }
    }
}
