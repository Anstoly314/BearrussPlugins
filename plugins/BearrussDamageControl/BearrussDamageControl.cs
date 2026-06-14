using System;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BearrussDamageControl", "Bearruss Plugins", "1.0.1")]
    [Description("PvE/PvP damage control with admin GUI for Rust servers.")]
    public class BearrussDamageControl : RustPlugin
    {
        private const string PermAdmin = "bearrussdamagecontrol.admin";
        private const string UiName = "BearrussDamageControl.UI";

        private PluginConfig _config;

        private class PluginConfig
        {
            public string Режим_сервера = "PVE";
            public string Команда_GUI = "dm";
            public bool Показывать_объявление_при_смене_режима = true;
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
        }

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

            if (IsPvpMode())
            {
                HandlePvpDamage(entity, info);
                return;
            }

            HandlePveDamage(entity, info);
        }

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
                BlockDamage(info);
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
                if (attackerPlayer != null && attackerPlayer != targetPlayer && !_config.Настройки_PVE.Игрок_может_получать_урон_от_игрока)
                {
                    BlockDamage(info);
                    return;
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

            if (IsToolCupboard(entity))
            {
                if (!_config.Настройки_PVE.Урон_по_TC_шкафу) BlockDamage(info);
                return;
            }

            if (IsDoor(entity))
            {
                if (!_config.Настройки_PVE.Урон_по_дверям)
                {
                    BlockDamage(info);
                    return;
                }

                if (_config.Настройки_PVE.Блокировать_урон_по_гаражной_двери && IsGarageDoor(entity))
                {
                    BlockDamage(info);
                    return;
                }

                if (_config.Настройки_PVE.Блокировать_урон_по_магазинной_витрине && IsShopfront(entity))
                {
                    BlockDamage(info);
                    return;
                }

                return;
            }

            if (IsWindowOrBars(entity))
            {
                if (!_config.Настройки_PVE.Урон_по_окнам_и_решеткам) BlockDamage(info);
                return;
            }

            if (IsTrap(entity))
            {
                if (!_config.Настройки_PVE.Урон_по_ловушкам) BlockDamage(info);
                return;
            }

            if (IsStorage(entity))
            {
                var locked = IsLockedStorage(entity);
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

                return;
            }

            if (IsVehicle(entity))
            {
                if (!_config.Настройки_PVE.Урон_по_транспорту) BlockDamage(info);
                return;
            }

            if (IsBuildingBlock(entity) && !_config.Настройки_PVE.Урон_по_постройкам)
                BlockDamage(info);
        }

        private void BlockDamage(HitInfo info)
        {
            info.damageTypes.ScaleAll(0f);
            info.DoHitEffects = false;
            info.HitMaterial = 0;
        }

        private bool IsPvpMode() => string.Equals(_config.Режим_сервера, "PVP", StringComparison.OrdinalIgnoreCase);
        private bool IsPveMode() => !IsPvpMode();

        private bool IsSelfDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            return player != null && info.InitiatorPlayer != null && player.userID == info.InitiatorPlayer.userID;
        }

        private bool IsNpc(BaseCombatEntity entity)
        {
            var player = entity as BasePlayer;
            if (player == null) return false;
            if (player.userID.IsSteamId()) return false;
            return player.IsNpc || player.ShortPrefabName.Contains("scientist") || player.ShortPrefabName.Contains("murderer") || player.ShortPrefabName.Contains("bandit");
        }

        private bool IsBuildingBlock(BaseCombatEntity entity) => entity is BuildingBlock;

        private bool IsToolCupboard(BaseCombatEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("cupboard") || name.Contains("tool cupboard");
        }

        private bool IsDoor(BaseCombatEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("door") || name.Contains("shutter") || name.Contains("shopfront");
        }

        private bool IsGarageDoor(BaseCombatEntity entity) => GetName(entity).Contains("garage");
        private bool IsShopfront(BaseCombatEntity entity) => GetName(entity).Contains("shopfront") || GetName(entity).Contains("shop.front");

        private bool IsWindowOrBars(BaseCombatEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("window") || name.Contains("embrasure") || name.Contains("bars") || name.Contains("barricade.metal");
        }

        private bool IsTrap(BaseCombatEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("trap") || name.Contains("turret") || name.Contains("sam_site") || name.Contains("flameturret") || name.Contains("guntrap");
        }

        private bool IsVehicle(BaseCombatEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("mini") || name.Contains("scraptransport") || name.Contains("rhib") || name.Contains("rowboat") || name.Contains("modularcar") || name.Contains("snowmobile") || name.Contains("submarine") || name.Contains("horse");
        }

        private bool IsTrainOrWagon(BaseCombatEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("train") || name.Contains("wagon") || name.Contains("locomotive") || name.Contains("workcart");
        }

        private bool IsElectrical(BaseCombatEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("electric") || name.Contains("switch") || name.Contains("generator") || name.Contains("battery") || name.Contains("solar") || name.Contains("windmill") || name.Contains("wiretool") || name.Contains("branch") || name.Contains("splitter") || name.Contains("counter") || name.Contains("timer") || name.Contains("memorycell") || name.Contains("orswitch") || name.Contains("andswitch") || name.Contains("xorswitch") || name.Contains("light");
        }

        private bool IsStorage(BaseCombatEntity entity) => entity is StorageContainer;

        private bool IsLockedStorage(BaseCombatEntity entity)
        {
            var storage = entity as StorageContainer;
            if (storage == null) return false;
            return storage.GetSlot(BaseEntity.Slot.Lock) != null;
        }

        private string GetName(BaseEntity entity)
        {
            if (entity == null) return string.Empty;
            return (entity.ShortPrefabName ?? string.Empty).ToLower();
        }

        private void CmdDamageMenu(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!HasAccess(player))
            {
                player.ChatMessage("У вас нет прав для открытия меню Damage Control.");
                return;
            }
            OpenUi(player);
        }

        private void ConsoleMode(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            var mode = arg.GetString(0, "PVE").ToUpper();
            if (mode != "PVE" && mode != "PVP") return;
            _config.Режим_сервера = mode;
            SaveConfig();
            if (_config.Показывать_объявление_при_смене_режима) BroadcastModeChanged();
            OpenUi(player);
        }

        private void ConsoleToggle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            var key = arg.GetString(0, string.Empty);
            ToggleSetting(key);
            SaveConfig();
            OpenUi(player);
        }

        private void ConsoleClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DestroyUi(player);
        }

        private bool HasAccess(BasePlayer player) => player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermAdmin));

        private void BroadcastModeChanged()
        {
            if (IsPvpMode())
                Server.Broadcast("<color=#ff3333>[СУДНЫЙ ДЕНЬ]</color> Включен режим PVP. Урон между игроками разрешен.");
            else
                Server.Broadcast("<color=#55ff55>[PVE]</color> Включен режим PVE. Урон между игроками заблокирован.");
        }

        private void ToggleSetting(string key)
        {
            var pve = _config.Настройки_PVE;
            var pvp = _config.Настройки_PVP;
            switch (key)
            {
                case "pve_player_damage": pve.Игрок_может_получать_урон_от_игрока = !pve.Игрок_может_получать_урон_от_игрока; break;
                case "pve_self": pve.Самоурон_разрешен = !pve.Самоурон_разрешен; break;
                case "pve_npc_to_player": pve.Урон_от_NPC_по_игроку = !pve.Урон_от_NPC_по_игроку; break;
                case "pve_player_to_npc": pve.Урон_игрока_по_NPC = !pve.Урон_игрока_по_NPC; break;
                case "pve_npc_to_npc": pve.Урон_NPC_по_NPC = !pve.Урон_NPC_по_NPC; break;
                case "pve_tc": pve.Урон_по_TC_шкафу = !pve.Урон_по_TC_шкафу; break;
                case "pve_building": pve.Урон_по_постройкам = !pve.Урон_по_постройкам; break;
                case "pve_doors": pve.Урон_по_дверям = !pve.Урон_по_дверям; break;
                case "pve_garage": pve.Блокировать_урон_по_гаражной_двери = !pve.Блокировать_урон_по_гаражной_двери; break;
                case "pve_shopfront": pve.Блокировать_урон_по_магазинной_витрине = !pve.Блокировать_урон_по_магазинной_витрине; break;
                case "pve_windows": pve.Урон_по_окнам_и_решеткам = !pve.Урон_по_окнам_и_решеткам; break;
                case "pve_traps": pve.Урон_по_ловушкам = !pve.Урон_по_ловушкам; break;
                case "pve_vehicles": pve.Урон_по_транспорту = !pve.Урон_по_транспорту; break;
                case "pve_wagons": pve.Блокировать_урон_по_вагонам = !pve.Блокировать_урон_по_вагонам; break;
                case "pve_electric": pve.Урон_по_электрике = !pve.Урон_по_электрике; break;
                case "pve_unlocked_boxes": pve.Урон_по_незалоченным_ящикам = !pve.Урон_по_незалоченным_ящикам; break;
                case "pve_locked_boxes": pve.Урон_по_залоченным_ящикам = !pve.Урон_по_залоченным_ящикам; break;
                case "pvp_all": pvp.Разрешить_весь_урон = !pvp.Разрешить_весь_урон; break;
                case "pvp_self": pvp.Самоурон_разрешен = !pvp.Самоурон_разрешен; break;
                case "pvp_electric": pvp.Защищать_электрику_даже_в_PVP = !pvp.Защищать_электрику_даже_в_PVP; break;
                case "pvp_wagons": pvp.Защищать_вагоны_даже_в_PVP = !pvp.Защищать_вагоны_даже_в_PVP; break;
            }
        }

        private void OpenUi(BasePlayer player)
        {
            DestroyUi(player);
            var c = new CuiElementContainer();
            var bg = c.Add(new CuiPanel
            {
                Image = { Color = _config.Настройки_GUI.Цвет_фона },
                RectTransform = { AnchorMin = "0.18 0.12", AnchorMax = "0.82 0.88" },
                CursorEnabled = true
            }, "Overlay", UiName);

            c.Add(new CuiLabel
            {
                Text = { Text = _config.Настройки_GUI.Заголовок + " | Режим: " + _config.Режим_сервера, FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 0.75 0.25 1" },
                RectTransform = { AnchorMin = "0.02 0.91", AnchorMax = "0.98 0.99" }
            }, bg);

            AddButton(c, bg, "PVE", IsPveMode() ? _config.Настройки_GUI.Цвет_кнопки_включено : _config.Настройки_GUI.Цвет_кнопки_обычная, "bearrussdamagecontrol.mode PVE", 0.05f, 0.83f, 0.30f, 0.90f);
            AddButton(c, bg, "PVP / Судный день", IsPvpMode() ? _config.Настройки_GUI.Цвет_кнопки_включено : _config.Настройки_GUI.Цвет_кнопки_обычная, "bearrussdamagecontrol.mode PVP", 0.34f, 0.83f, 0.64f, 0.90f);
            AddButton(c, bg, "X", "0.55 0.05 0.05 0.95", "bearrussdamagecontrol.close", 0.92f, 0.92f, 0.98f, 0.985f);

            AddSection(c, bg, "PVE категории", 0.05f, 0.76f, 0.48f, 0.81f);
            AddSection(c, bg, "PVP категории", 0.52f, 0.76f, 0.95f, 0.81f);

            var y = 0.70f;
            AddToggle(c, bg, "Игрок получает урон от игрока", _config.Настройки_PVE.Игрок_может_получать_урон_от_игрока, "pve_player_damage", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "Самоурон", _config.Настройки_PVE.Самоурон_разрешен, "pve_self", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "NPC → игрок", _config.Настройки_PVE.Урон_от_NPC_по_игроку, "pve_npc_to_player", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "Игрок → NPC", _config.Настройки_PVE.Урон_игрока_по_NPC, "pve_player_to_npc", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "NPC → NPC", _config.Настройки_PVE.Урон_NPC_по_NPC, "pve_npc_to_npc", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "TC шкаф", _config.Настройки_PVE.Урон_по_TC_шкафу, "pve_tc", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "Постройки", _config.Настройки_PVE.Урон_по_постройкам, "pve_building", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "Двери", _config.Настройки_PVE.Урон_по_дверям, "pve_doors", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "Защита гаражных дверей", _config.Настройки_PVE.Блокировать_урон_по_гаражной_двери, "pve_garage", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "Защита витрин", _config.Настройки_PVE.Блокировать_урон_по_магазинной_витрине, "pve_shopfront", 0.05f, y); y -= 0.055f;
            AddToggle(c, bg, "Окна / решетки", _config.Настройки_PVE.Урон_по_окнам_и_решеткам, "pve_windows", 0.05f, y);

            y = 0.70f;
            AddToggle(c, bg, "Ловушки", _config.Настройки_PVE.Урон_по_ловушкам, "pve_traps", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "Транспорт", _config.Настройки_PVE.Урон_по_транспорту, "pve_vehicles", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "Защита вагонов", _config.Настройки_PVE.Блокировать_урон_по_вагонам, "pve_wagons", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "Электрика", _config.Настройки_PVE.Урон_по_электрике, "pve_electric", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "Незалоченные ящики", _config.Настройки_PVE.Урон_по_незалоченным_ящикам, "pve_unlocked_boxes", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "Залоченные ящики", _config.Настройки_PVE.Урон_по_залоченным_ящикам, "pve_locked_boxes", 0.52f, y); y -= 0.075f;
            AddToggle(c, bg, "PVP: весь урон", _config.Настройки_PVP.Разрешить_весь_урон, "pvp_all", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "PVP: самоурон", _config.Настройки_PVP.Самоурон_разрешен, "pvp_self", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "PVP: защита электрики", _config.Настройки_PVP.Защищать_электрику_даже_в_PVP, "pvp_electric", 0.52f, y); y -= 0.055f;
            AddToggle(c, bg, "PVP: защита вагонов", _config.Настройки_PVP.Защищать_вагоны_даже_в_PVP, "pvp_wagons", 0.52f, y);

            c.Add(new CuiLabel
            {
                Text = { Text = "Команда: /dm | Право: bearrussdamagecontrol.admin | Автор: Bearruss Plugins", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.9 0.9 0.9 0.75" },
                RectTransform = { AnchorMin = "0.05 0.02", AnchorMax = "0.95 0.06" }
            }, bg);

            CuiHelper.AddUi(player, c);
        }

        private void AddSection(CuiElementContainer c, string parent, string text, float x1, float y1, float x2, float y2)
        {
            c.Add(new CuiPanel
            {
                Image = { Color = _config.Настройки_GUI.Цвет_панели },
                RectTransform = { AnchorMin = $"{x1} {y1}", AnchorMax = $"{x2} {y2}" }
            }, parent);

            c.Add(new CuiLabel
            {
                Text = { Text = text, FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 0.85 0.35 1" },
                RectTransform = { AnchorMin = $"{x1} {y1}", AnchorMax = $"{x2} {y2}" }
            }, parent);
        }

        private void AddToggle(CuiElementContainer c, string parent, string label, bool state, string key, float x, float y)
        {
            c.Add(new CuiLabel
            {
                Text = { Text = label, FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.95" },
                RectTransform = { AnchorMin = $"{x} {y}", AnchorMax = $"{x + 0.28f} {y + 0.045f}" }
            }, parent);

            AddButton(c, parent, state ? "ВКЛ" : "ВЫКЛ", state ? _config.Настройки_GUI.Цвет_кнопки_включено : _config.Настройки_GUI.Цвет_кнопки_выключено, $"bearrussdamagecontrol.toggle {key}", x + 0.29f, y, x + 0.42f, y + 0.045f);
        }

        private void AddButton(CuiElementContainer c, string parent, string text, string color, string command, float x1, float y1, float x2, float y2)
        {
            c.Add(new CuiButton
            {
                Button = { Color = color, Command = command },
                Text = { Text = text, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = $"{x1} {y1}", AnchorMax = $"{x2} {y2}" }
            }, parent);
        }

        private void DestroyUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiName);
        }
    }
}
