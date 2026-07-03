using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT.UI;
using HarmonyLib;
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
        private const string PluginVersion = "1.0.0";

        private const string TabControllerFieldName = "gclass3808_0";

        private static ConfigEntry<KeyboardShortcut> _nextTabKey;
        private static ConfigEntry<KeyboardShortcut> _prevTabKey;

        private FieldInfo _tabControllerField;
        private bool _reflectionFailed;

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

            Logger.LogInfo("[InventoryTabsHotkey] Successfully Loaded.");
        }

        private void Update()
        {
            if (_reflectionFailed)
            {
                return;
            }

            if (_nextTabKey.Value.IsDown())
            {
                ShiftTab(1);
            }
            else if (_prevTabKey.Value.IsDown())
            {
                ShiftTab(-1);
            }
        }

        private void ShiftTab(int direction)
        {
            if (IsTypingInTextField())
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

        private bool IsTypingInTextField()
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && selected.GetComponent<TMPro.TMP_InputField>() != null;
        }
    }
}