using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
[Info(“BearrussDamageControl”, “Bearruss Plugins”, “1.0.0”)]
[Description(“PvE/PvP damage control with admin GUI for Rust servers.”)]
public class BearrussDamageControl : RustPlugin
{
private const string PermAdmin = “bearrussdamagecontrol.admin”;
private const string UiName = “BearrussDamageControl.UI”;

    private PluginConfig _config;
    #region Config
    private class PluginConfig
    {
        public string Режим_сервера = "PVE";
        public string Команда_GUI = "dm";
        public bool Показывать_оверлей_судный_день_в_PVP = true;
        public PveRules Настройки_PVE = new PveRules();
        public PvpRules Настройки_PVP = new PvpRules();
        public GuiSettings Настройки_GUI = new GuiSettings();
    }
    private class PveRules
    {
        public bool Игрок_может_получать_урон_от_игрока = false;
        public bool Самоурон_разрешен = true;
        public bool Урон_от_NPC_по_игроку = true;
        public bool Урон_игрока_по_NPC = true;
        public bool Урон_NPC_по_NPC = true;
        public bool Урон_по_TC_шкафу = true;
        public bool Урон_по_постройкам = false;
        public bool Урон_по_дверям = true;
        public bool Блокировать_урон_по_гаражной_двери = true;
        public bool Блокировать_урон_по_магазинной_витрине = true;
        public bool Урон_по_окнам_и_решеткам = false;
        public bool Урон_по_ловушкам = false;
        public bool Урон_по_транспорту = true;
        public bool Блокировать_урон_по_вагонам = true;
        public bool Урон_по_электрике = false;
        public bool Урон_по_незалоченным_ящикам = true;
        public bool Урон_по_залоченным_ящикам = false;
    }
    private class PvpRules
    {
        public bool Разрешить_весь_урон = true;
        public bool Самоурон_разрешен = true;
        public bool Защищать_электрику_даже_в_PVP = false;
        public bool Защищать_вагоны_даже_в_PVP = false;
            private class GuiSettings
        {
            public string Цвет_фона = "0.05 0.05 0.05 0.92";
            public string Цвет_панели = "0.12 0.02 0.02 0.95";
            public string Цвет_кнопки_включено = "0.15 0.55 0.18 0.95";
            public string Цвет_кнопки_выключено = "0.55 0.12 0.12 0.95";
            public string Цвет_кнопки_обычная = "0.35 0.18 0.04 0.95";
            public string Заголовок = "BEARRUSS DAMAGE CONTROL";
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) throw new Exception("Config is null");
            }
            catch
            {
                PrintWarning("Конфиг поврежден. Создаю новый конфиг по умолчанию.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
            cmd.AddChatCommand(_config?.Команда_GUI ?? "dm", this, nameof(CmdDamageMenu));
            cmd.AddConsoleCommand("bearrussdamagecontrol.mode", this, nameof(ConsoleMode));
            cmd.AddConsoleCommand("bearrussdamagecontrol.toggle", this, nameof(ConsoleToggle));
            cmd.AddConsoleCommand("bearrussdamagecontrol.close", this, nameof(ConsoleClose));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUi(player);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            if (info.Initiator == null && info.WeaponPrefab == null) return;

            if (IsPvpMode())
            {
                HandlePvpDamage(entity, info);
                return;
            }

            HandlePveDamage(entity, info);
        }

        #endregion

        #region Damage Logic

        private void HandlePvpDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!_config.Настройки_PVP.Разрешить_весь_урон)
            {
                HandlePveDamage(entity, info);
                return;
            }

            if (!_config.Настройки_PVP.Самоурон_разрешен && IsSelfDamage(entity, info))
            {
                BlockDamage(info);
                return;
            }

            if (_config.Настройки_PVP.Защищать_электрику_даже_в_PVP && IsElectrical(entity))
            {
                BlockDamage(info);
                return;
            }

            if (_config.Настройки_PVP.Защищать_вагоны_даже_в_PVP && IsTrainOrWagon(entity))
            {
                BlockDamage(info);
            }
        }

        private void HandlePveDamage(BaseCombatEntity entity, HitInfo info)
        {
            var targetPlayer = entity as BasePlayer;
            var attackerPlayer = info.InitiatorPlayer;
            var attackerEntity = info.Initiator as BaseCombatEntity;

            if (!_config.Настройки_PVE.Самоурон_разрешен && IsSelfDamage(entity, info))
            {
                BlockDamage(info);
                return;
            }

            if (targetPlayer != null)
            {
                if (attackerPlayer != null && attackerPlayer != targetPlayer)
                {
                    if (!_config.Настройки_PVE.Игрок_может_получать_урон_от_игрока)
                    {
                        BlockDamage(info);
                        return;
                    }
                }

                if (IsNpc(attackerEntity) && !_config.Настройки_PVE.Урон_от_NPC_по_игроку)
                {
                    BlockDamage(info);
                    return;
                }
            }

            if (IsNpc(entity))
            {
                if (attackerPlayer != null && !_config.Настройки_PVE.Урон_игрока_по_NPC)
                {
                    BlockDamage(info);
                    return;
                }

                if (IsNpc(attackerEntity) && !_config.Настройки_PVE.Урон_NPC_по_NPC)
                {
                    BlockDamage(info);
                    return;
                }

                return;
            }

            if (IsTrainOrWagon(entity) && _config.Настройки_PVE.Блокировать_урон_по_вагонам)
            {
                BlockDamage(info);
                return;
            }

            if (IsElectrical(entity) && !_config.Настройки_PVE.Урон_по_электрике)
            {
                BlockDamage(info);
                return;
            }
            if (IsStorage(entity))
            {
                bool locked = IsLockedStorage(entity);

                if (locked && !_config.Настройки_PVE.Урон_по_залоченным_ящикам)
                {
                    BlockDamage(info);
                    return;
                }

                if (!locked && !_config.Настройки_PVE.Урон_по_незалоченным_ящикам)
                {
                    BlockDamage(info);
                    return;
                }
            }

            if (IsGarageDoor(entity) && _config.Настройки_PVE.Блокировать_урон_по_гаражной_двери)
            {
                BlockDamage(info);
                return;
            }

            if (IsShopFront(entity) && _config.Настройки_PVE.Блокировать_урон_по_магазинной_витрине)
            {
                BlockDamage(info);
                return;
            }

            if (IsVehicle(entity) && !_config.Настройки_PVE.Урон_по_транспорту)
            {
                BlockDamage(info);
                return;
            }

            if (IsBuilding(entity) && !_config.Настройки_PVE.Урон_по_постройкам)
            {
                BlockDamage(info);
                return;
            }

            if (IsToolCupboard(entity) && !_config.Настройки_PVE.Урон_по_TC_шкафу)
            {
                BlockDamage(info);
                return;
            }
        }

        private void BlockDamage(HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.DoHitEffects = false;
            info.HitMaterial = 0;
        }

        #endregion

        #region Helpers

        private bool IsPvpMode() =>
            _config.Режим_сервера.Equals("PVP", StringComparison.OrdinalIgnoreCase);

        private bool IsSelfDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            return player != null && info.InitiatorPlayer == player;
        }

        private bool IsNpc(BaseCombatEntity entity)
        {
            if (entity == null) return false;

            return entity is NPCPlayer ||
                   entity.ShortPrefabName.Contains("scientist") ||
                   entity.ShortPrefabName.Contains("murderer") ||
                   entity.ShortPrefabName.Contains("bandit");
        }

        private bool IsBuilding(BaseCombatEntity entity)
        {
            return entity is BuildingBlock;
        }

        private bool IsToolCupboard(BaseCombatEntity entity)
        {
            return entity.ShortPrefabName.Contains("cupboard");
        }

        private bool IsGarageDoor(BaseCombatEntity entity)
        {
            return entity.ShortPrefabName.Contains("garage");
        }

        private bool IsShopFront(BaseCombatEntity entity)
        {
            return entity.ShortPrefabName.Contains("shopfront");
        }

        private bool IsVehicle(BaseCombatEntity entity)
        {
            string name = entity.ShortPrefabName;

            return name.Contains("mini") ||
                   name.Contains("scraptransport") ||
                   name.Contains("rhib") ||
                   name.Contains("rowboat") ||
                   name.Contains("modularcar");
        }

        private bool IsTrainOrWagon(BaseCombatEntity entity)
        {
            string name = entity.ShortPrefabName;

            return name.Contains("train") ||
                   name.Contains("wagon");
        }

        private bool IsElectrical(BaseCombatEntity entity)
        {
            string name = entity.ShortPrefabName;

            return name.Contains("electrical") ||
                   name.Contains("switch") ||
                   name.Contains("generator") ||
                   name.Contains("battery");
        }

        private bool IsStorage(BaseCombatEntity entity)
        {
            return entity is StorageContainer;
        }

        private bool IsLockedStorage(BaseCombatEntity entity)
        {
            var storage = entity as StorageContainer;
            if (storage == null) return false;

            return storage.GetSlot(BaseEntity.Slot.Lock) != null;
        }

        #endregion
        #region Commands and GUI

        private void CmdDamageMenu(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                player.ChatMessage("У вас нет прав для открытия меню Damage Control.");
                return;
            }

            OpenMainUi(player);
        }

        private void ConsoleMode(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin)) return;

            string mode = arg.GetString(0, "PVE").ToUpper();
            if (mode != "PVE" && mode != "PVP") return;

            _config.Режим_сервера = mode;
            SaveConfig();
            OpenMainUi(player);
        }

        private void ConsoleToggle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin)) return;

            string key = arg.GetString(0, "");
            ToggleSetting(key);
            SaveConfig();
            OpenMainUi(player);
        }

        private void ConsoleClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DestroyUi(player);
        }

        private void ToggleSetting(string key)
        {
            var pve = _config.Настройки_PVE;

            switch (key)
            {
                case "self": pve.Самоурон_разрешен = !pve.Самоурон_разрешен; break;
                case "playerdamage": pve.Игрок_может_получать_урон_от_игрока = !pve.Игрок_может_получать_урон_от_игрока; break;
                case "npcplayer": pve.Урон_от_NPC_по_игроку = !pve.Урон_от_NPC_по_игроку; break;
                case "playernpc": pve.Урон_игрока_по_NPC = !pve.Урон_игрока_по_NPC; break;
                case "npcnpc": pve.Урон_NPC_по_NPC = !pve.Урон_NPC_по_NPC; break;
                case "tc": pve.Урон_по_TC_шкафу = !pve.Урон_по_TC_шкафу; break;
                case "building": pve.Урон_по_постройкам = !pve.Урон_по_постройкам; break;
                case "doors": pve.Урон_по_дверям = !pve.Урон_по_дверям; break;
                case "garage": pve.Блокировать_урон_по_гаражной_двери = !pve.Блокировать_урон_по_гаражной_двери; break;
                case "shopfront": pve.Блокировать_урон_по_магазинной_витрине = !pve.Блокировать_урон_по_магазинной_витрине; break;
                case "windows": pve.Урон_по_окнам_и_решеткам = !pve.Урон_по_окнам_и_решеткам; break;
                case "traps": pve.Урон_по_ловушкам = !pve.Урон_по_ловушкам; break;
                case "vehicles": pve.Урон_по_транспорту = !pve.Урон_по_транспорту; break;
                case "wagons": pve.Блокировать_урон_по_вагонам = !pve.Блокировать_урон_по_вагонам; break;
                case "electric": pve.Урон_по_электрике = !pve.Урон_по_электрике; break;
                case "unlockedbox": pve.Урон_по_незалоченным_ящикам = !pve.Урон_по_незалоченным_ящикам; break;
                case "lockedbox": pve.Урон_по_залоченным_ящикам = !pve.Урон_по_залоченным_ящикам; break;
            }
        }

        private void OpenMainUi(BasePlayer player)
        {
            DestroyUi(player);

            var gui = _config.Настройки_GUI;
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = gui.Цвет_фона },
                RectTransform = { AnchorMin = "0.18 0.12", AnchorMax = "0.82 0.88" },
                CursorEnabled = true
            }, "Overlay", UiName);

            container.Add(new CuiLabel
            {
                Text = { Text = $"{gui.Заголовок}\nТекущий режим: {_config.Режим_сервера}", FontSize = 22, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.05 0.86", AnchorMax = "0.95 0.98" }
            }, UiName);

            AddButton(container, UiName, "PVE", "bearrussdamagecontrol.mode PVE", "0.08 0.76", "0.30 0.84", _config.Режим_сервера == "PVE");
            AddButton(container, UiName, "PVP", "bearrussdamagecontrol.mode PVP", "0.32 0.76", "0.54 0.84", _config.Режим_сервера == "PVP");
            AddButton(container, UiName, "ЗАКРЫТЬ", "bearrussdamagecontrol.close", "0.72 0.76", "0.92 0.84", false);

            float y = 0.66f;
            float h = 0.055f;

            AddToggle(container, "Самоурон", "self", _config.Настройки_PVE.Самоурон_разрешен, 0.08f, y, h);
            AddToggle(container, "Игрок → Игрок", "playerdamage", _config.Настройки_PVE.Игрок_может_получать_урон_от_игрока, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "NPC → Игрок", "npcplayer", _config.Настройки_PVE.Урон_от_NPC_по_игроку, 0.08f, y, h);
            AddToggle(container, "Игрок → NPC", "playernpc", _config.Настройки_PVE.Урон_игрока_по_NPC, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "NPC → NPC", "npcnpc", _config.Настройки_PVE.Урон_NPC_по_NPC, 0.08f, y, h);
            AddToggle(container, "TC шкаф", "tc", _config.Настройки_PVE.Урон_по_TC_шкафу, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "Постройки", "building", _config.Настройки_PVE.Урон_по_постройкам, 0.08f, y, h);
            AddToggle(container, "Двери", "doors", _config.Настройки_PVE.Урон_по_дверям, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "Блок гаражных дверей", "garage", _config.Настройки_PVE.Блокировать_урон_по_гаражной_двери, 0.08f, y, h);
            AddToggle(container, "Блок витрин", "shopfront", _config.Настройки_PVE.Блокировать_урон_по_магазинной_витрине, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "Окна и решетки", "windows", _config.Настройки_PVE.Урон_по_окнам_и_решеткам, 0.08f, y, h);
            AddToggle(container, "Ловушки", "traps", _config.Настройки_PVE.Урон_по_ловушкам, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "Транспорт", "vehicles", _config.Настройки_PVE.Урон_по_транспорту, 0.08f, y, h);
            AddToggle(container, "Блок вагонов", "wagons", _config.Настройки_PVE.Блокировать_урон_по_вагонам, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "Электрика", "electric", _config.Настройки_PVE.Урон_по_электрике, 0.08f, y, h);
            AddToggle(container, "Незалоченные ящики", "unlockedbox", _config.Настройки_PVE.Урон_по_незалоченным_ящикам, 0.52f, y, h);

            y -= 0.07f;
            AddToggle(container, "Залоченные ящики", "lockedbox", _config.Настройки_PVE.Урон_по_залоченным_ящикам, 0.08f, y, h);

            CuiHelper.AddUi(player, container);
        }

        private void AddToggle(CuiElementContainer container, string text, string key, bool state, float x, float y, float h)
        {
            string label = $"{text}: {(state ? "ВКЛ" : "ВЫКЛ")}";
            string min = $"{x} {y}";
            string max = $"{x + 0.38f} {y + h}";

            AddButton(container, UiName, label, $"bearrussdamagecontrol.toggle {key}", min, max, state);
        }

        private void AddButton(CuiElementContainer container, string parent, string text, string command, string min, string max, bool active)
        {
            var gui = _config.Настройки_GUI;

            container.Add(new CuiButton
            {
                Button = { Color = active ? gui.Цвет_кнопки_включено : gui.Цвет_кнопки_выключено, Command = command },
                RectTransform = { AnchorMin = min, AnchorMax = max },
                Text = { Text = text, FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, parent);
        }

        private void DestroyUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiName);
        }

        #endregion
    }
}