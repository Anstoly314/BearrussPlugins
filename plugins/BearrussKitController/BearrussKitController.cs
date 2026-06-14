using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BearrussKitController", "Bearruss Plugins", "1.0.0")]
    [Description("Independent kit controller with player/admin GUI and inventory copy.")]
    public class BearrussKitController : RustPlugin
    {
        private const string PermAdmin = "bearrusskitcontroller.admin";
        private const string UiPlayer = "BearrussKitController.PlayerUI";
        private const string UiAdmin = "BearrussKitController.AdminUI";

        private PluginConfig _config;
        private StoredData _data;

        #region Config/Data

        private class PluginConfig
        {
            public string Команда_игрока = "kit";
            public string Команда_админа = "kitadmin";
            public string Фон_GUI_ссылка = "";
            public string Цвет_фона_если_нет_картинки = "0.04 0.04 0.04 0.95";
            public string Цвет_панели = "0.10 0.02 0.02 0.90";
            public string Цвет_кнопки = "0.45 0.18 0.04 0.95";
            public string Цвет_кнопки_нельзя = "0.35 0.35 0.35 0.75";
            public string Цвет_кнопки_получить = "0.12 0.50 0.16 0.95";
            public string Заголовок_GUI = "BEARRUSS KIT CONTROLLER";
            public string Shortname_сопли = "supply.signal";
            public int Количество_соплей = 1;
            public int Сколько_первых_респавнов_выдавать_соплю = 3;
            public List<KitDefinition> Киты = new List<KitDefinition>();
        }

        private class KitDefinition
        {
            public string ID = "start";
            public string Название = "Стартовый";
            public string Описание = "Стартовый кит";
            public string Картинка_ссылка = "";
            public string Permission = "";
            public int Кулдаун_секунд = 86400;
            public int Лимит_использований_за_вайп = 0;
            public List<KitItem> Предметы = new List<KitItem>();
        }

        private class KitItem
        {
            public string Shortname;
            public int Amount;
            public ulong SkinId;
            public float Condition;
            public int Slot;
            public string Container;
        }

        private class StoredData
        {
            public Dictionary<ulong, PlayerKitData> Игроки = new Dictionary<ulong, PlayerKitData>();
        }

        private class PlayerKitData
        {
            public int Количество_респавнов;
            public Dictionary<string, double> Последнее_получение = new Dictionary<string, double>();
            public Dictionary<string, int> Использовано = new Dictionary<string, int>();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
            _config.Киты.Add(new KitDefinition
            {
                ID = "start",
                Название = "Стартовый",
                Описание = "Первый набор игрока. Настрой через /kitadmin.",
                Кулдаун_секунд = 86400,
                Лимит_использований_за_вайп = 0,
                Картинка_ссылка = "",
                Permission = "",
                Предметы = new List<KitItem>
                {
                    new KitItem { Shortname = "stonehatchet", Amount = 1, SkinId = 0, Condition = 0f, Slot = 0, Container = "belt" },
                    new KitItem { Shortname = "stone.pickaxe", Amount = 1, SkinId = 0, Condition = 0f, Slot = 1, Container = "belt" }
                }
            });
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
                PrintWarning("Конфиг поврежден. Создаю новый.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void LoadData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (_data == null) _data = new StoredData();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
            cmd.AddChatCommand(_config?.Команда_игрока ?? "kit", this, nameof(CmdKit));
            cmd.AddChatCommand(_config?.Команда_админа ?? "kitadmin", this, nameof(CmdKitAdmin));
            cmd.AddConsoleCommand("bearrusskit.close", this, nameof(ConsoleClose));
            cmd.AddConsoleCommand("bearrusskit.claim", this, nameof(ConsoleClaim));
            cmd.AddConsoleCommand("bearrusskit.admin.create", this, nameof(ConsoleAdminCreate));
            cmd.AddConsoleCommand("bearrusskit.admin.delete", this, nameof(ConsoleAdminDelete));
            cmd.AddConsoleCommand("bearrusskit.admin.open", this, nameof(ConsoleAdminOpen));
            LoadData();
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyPlayerUi(player);
                DestroyAdminUi(player);
            }
            SaveData();
        }

        private void OnServerSave() => SaveData();

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || _config == null) return;
            timer.Once(1f, () => GiveNewPlayerSignal(player));
        }

        #endregion

        #region Commands

        private void CmdKit(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            OpenPlayerUi(player);
        }

        private void CmdKitAdmin(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!HasAdmin(player))
            {
                player.ChatMessage("Нет прав: bearrusskitcontroller.admin");
                return;
            }
            OpenAdminUi(player);
        }

        private void ConsoleClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DestroyPlayerUi(player);
            DestroyAdminUi(player);
        }

        private void ConsoleClaim(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var id = arg.GetString(0, string.Empty);
            ClaimKit(player, id);
        }

        private void ConsoleAdminOpen(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdmin(player)) return;
            OpenAdminUi(player);
        }

        private void ConsoleAdminCreate(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdmin(player)) return;
            CreateKitFromInventory(player);
            OpenAdminUi(player);
        }

        private void ConsoleAdminDelete(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdmin(player)) return;
            var id = arg.GetString(0, string.Empty);
            var kit = FindKit(id);
            if (kit == null)
            {
                player.ChatMessage("Кит не найден.");
                return;
            }
            _config.Киты.Remove(kit);
            SaveConfig();
            player.ChatMessage($"Кит {id} удалён.");
            OpenAdminUi(player);
        }

        #endregion

        #region Kit Logic

        private void ClaimKit(BasePlayer player, string id)
        {
            var kit = FindKit(id);
            if (kit == null)
            {
                player.ChatMessage("Кит не найден.");
                return;
            }

            var reason = GetBlockReason(player, kit);
            if (!string.IsNullOrEmpty(reason))
            {
                player.ChatMessage(reason);
                OpenPlayerUi(player);
                return;
            }

            foreach (var kitItem in kit.Предметы)
                GiveKitItem(player, kitItem);

            var pd = GetPlayerData(player.userID);
            pd.Последнее_получение[kit.ID] = Now();
            if (!pd.Использовано.ContainsKey(kit.ID)) pd.Использовано[kit.ID] = 0;
            pd.Использовано[kit.ID]++;
            SaveData();
            player.ChatMessage($"Вы получили кит: {kit.Название}");
            OpenPlayerUi(player);
        }

        private void CreateKitFromInventory(BasePlayer player)
        {
            var kit = new KitDefinition();
            kit.ID = GetNextKitId();
            kit.Название = "Кит " + kit.ID;
            kit.Описание = "Создан из инвентаря администратора";
            kit.Картинка_ссылка = "";
            kit.Permission = "";
            kit.Кулдаун_секунд = 86400;
            kit.Лимит_использований_за_вайп = 0;
            kit.Предметы = SnapshotInventory(player);
            _config.Киты.Add(kit);
            SaveConfig();
            player.ChatMessage($"Создан кит {kit.ID}. Предметов: {kit.Предметы.Count}. Название, картинку и permission настрой в конфиге.");
        }

        private List<KitItem> SnapshotInventory(BasePlayer player)
        {
            var list = new List<KitItem>();
            CopyContainer(player.inventory.containerBelt, "belt", list);
            CopyContainer(player.inventory.containerMain, "main", list);
            CopyContainer(player.inventory.containerWear, "wear", list);
            return list;
        }

        private void CopyContainer(ItemContainer container, string name, List<KitItem> list)
        {
            if (container == null) return;
            foreach (var item in container.itemList)
            {
                if (item == null || item.info == null) continue;
                list.Add(new KitItem
                {
                    Shortname = item.info.shortname,
                    Amount = item.amount,
                    SkinId = item.skin,
                    Condition = item.hasCondition ? item.condition : 0f,
                    Slot = item.position,
                    Container = name
                });
            }
        }

        private void GiveKitItem(BasePlayer player, KitItem kitItem)
        {
            if (kitItem == null || string.IsNullOrEmpty(kitItem.Shortname) || kitItem.Amount <= 0) return;
            var item = ItemManager.CreateByName(kitItem.Shortname, kitItem.Amount, kitItem.SkinId);
            if (item == null) return;
            if (kitItem.Condition > 0f && item.hasCondition) item.condition = Math.Min(kitItem.Condition, item.maxCondition);

            var container = GetContainer(player, kitItem.Container);
            if (container != null && kitItem.Slot >= 0 && item.MoveToContainer(container, kitItem.Slot)) return;
            if (container != null && item.MoveToContainer(container)) return;
            player.GiveItem(item);
        }

        private void GiveNewPlayerSignal(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            var limit = _config.Сколько_первых_респавнов_выдавать_соплю;
            if (limit <= 0) return;
            var pd = GetPlayerData(player.userID);
            if (pd.Количество_респавнов >= limit) return;
            pd.Количество_респавнов++;
            var item = ItemManager.CreateByName(_config.Shortname_сопли, _config.Количество_соплей);
            if (item != null) player.GiveItem(item);
            SaveData();
        }

        private string GetBlockReason(BasePlayer player, KitDefinition kit)
        {
            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
                return "У вас нет доступа к этому киту.";

            var pd = GetPlayerData(player.userID);
            if (kit.Лимит_использований_за_вайп > 0)
            {
                var used = pd.Использовано.ContainsKey(kit.ID) ? pd.Использовано[kit.ID] : 0;
                if (used >= kit.Лимит_использований_за_вайп) return "Лимит использований этого кита за вайп исчерпан.";
            }

            if (kit.Кулдаун_секунд > 0 && pd.Последнее_получение.ContainsKey(kit.ID))
            {
                var left = kit.Кулдаун_секунд - (Now() - pd.Последнее_получение[kit.ID]);
                if (left > 0) return "Кит будет доступен через: " + FormatTime(left);
            }

            return null;
        }

        #endregion

        #region GUI

        private void OpenPlayerUi(BasePlayer player)
        {
            DestroyPlayerUi(player);
            var c = new CuiElementContainer();
            var root = AddRoot(c, UiPlayer);
            AddHeader(c, root, "ДОСТУПНЫЕ КИТЫ", "bearrusskit.close");

            var kits = _config.Киты;
            var x = 0.06f;
            var y = 0.68f;
            var w = 0.27f;
            var h = 0.20f;
            var gapX = 0.035f;
            var gapY = 0.04f;

            for (var i = 0; i < kits.Count && i < 9; i++)
            {
                var kit = kits[i];
                var col = i % 3;
                var row = i / 3;
                AddKitCard(c, root, kit, x + col * (w + gapX), y - row * (h + gapY), w, h, player);
            }

            CuiHelper.AddUi(player, c);
        }

        private void OpenAdminUi(BasePlayer player)
        {
            DestroyAdminUi(player);
            var c = new CuiElementContainer();
            var root = AddRoot(c, UiAdmin);
            AddHeader(c, root, "АДМИН-ПАНЕЛЬ КИТОВ", "bearrusskit.close");

            AddButton(c, root, "Создать кит из моего инвентаря", "bearrusskit.admin.create", "0.06 0.78", "0.48 0.86", _config.Цвет_кнопки_получить);
            AddButton(c, root, "Открыть меню игрока", "bearrusskit.close; chat.say /kit", "0.52 0.78", "0.86 0.86", _config.Цвет_кнопки);

            c.Add(new CuiLabel
            {
                Text = { Text = "После создания настрой название, картинку, permission, кулдаун и лимит в конфиге. Картинки используются только по твоим ссылкам.", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.85" },
                RectTransform = { AnchorMin = "0.06 0.69", AnchorMax = "0.94 0.76" }
            }, root);

            var y = 0.60f;
            foreach (var kit in _config.Киты)
            {
                c.Add(new CuiLabel
                {
                    Text = { Text = $"{kit.ID} | {kit.Название} | предметов: {kit.Предметы.Count}", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.9" },
                    RectTransform = { AnchorMin = $"0.08 {y}", AnchorMax = $"0.68 {y + 0.045f}" }
                }, root);
                AddButton(c, root, "Удалить", $"bearrusskit.admin.delete {kit.ID}", $"0.72 {y}", $"0.88 {y + 0.045f}", "0.55 0.05 0.05 0.95");
                y -= 0.055f;
                if (y < 0.12f) break;
            }

            CuiHelper.AddUi(player, c);
        }

        private string AddRoot(CuiElementContainer c, string name)
        {
            var root = c.Add(new CuiPanel
            {
                Image = { Color = _config.Цвет_фона_если_нет_картинки },
                RectTransform = { AnchorMin = "0.16 0.10", AnchorMax = "0.84 0.90" },
                CursorEnabled = true
            }, "Overlay", name);

            if (!string.IsNullOrEmpty(_config.Фон_GUI_ссылка))
            {
                c.Add(new CuiElement
                {
                    Parent = root,
                    Components =
                    {
                        new CuiRawImageComponent { Url = _config.Фон_GUI_ссылка, Color = "1 1 1 0.55" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }

            c.Add(new CuiPanel
            {
                Image = { Color = _config.Цвет_панели },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, root);

            return root;
        }

        private void AddHeader(CuiElementContainer c, string root, string title, string closeCommand)
        {
            c.Add(new CuiLabel
            {
                Text = { Text = _config.Заголовок_GUI + " | " + title, FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 0.78 0.25 1" },
                RectTransform = { AnchorMin = "0.06 0.90", AnchorMax = "0.94 0.985" }
            }, root);
            AddButton(c, root, "X", closeCommand, "0.94 0.93", "0.985 0.985", "0.55 0.05 0.05 0.95");
        }

        private void AddKitCard(CuiElementContainer c, string root, KitDefinition kit, float x, float y, float w, float h, BasePlayer player)
        {
            c.Add(new CuiPanel
            {
                Image = { Color = "0.02 0.02 0.02 0.82" },
                RectTransform = { AnchorMin = $"{x} {y}", AnchorMax = $"{x + w} {y + h}" }
            }, root, root + ".card" + kit.ID);

            var card = root + ".card" + kit.ID;
            if (!string.IsNullOrEmpty(kit.Картинка_ссылка))
            {
                c.Add(new CuiElement
                {
                    Parent = card,
                    Components =
                    {
                        new CuiRawImageComponent { Url = kit.Картинка_ссылка, Color = "1 1 1 0.75" },
                        new CuiRectTransformComponent { AnchorMin = "0.04 0.28", AnchorMax = "0.96 0.96" }
                    }
                });
            }

            c.Add(new CuiLabel
            {
                Text = { Text = kit.Название, FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 0.85 0.35 1" },
                RectTransform = { AnchorMin = "0.04 0.74", AnchorMax = "0.96 0.96" }
            }, card);

            var reason = GetBlockReason(player, kit);
            var can = string.IsNullOrEmpty(reason);
            AddButton(c, card, can ? "Получить" : "Недоступно", can ? $"bearrusskit.claim {kit.ID}" : "", "0.08 0.05", "0.92 0.22", can ? _config.Цвет_кнопки_получить : _config.Цвет_кнопки_нельзя);
        }

        private void AddButton(CuiElementContainer c, string parent, string text, string command, string min, string max, string color)
        {
            c.Add(new CuiButton
            {
                Button = { Color = color, Command = command },
                Text = { Text = text, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = min, AnchorMax = max }
            }, parent);
        }

        private void DestroyPlayerUi(BasePlayer player) => CuiHelper.DestroyUi(player, UiPlayer);
        private void DestroyAdminUi(BasePlayer player) => CuiHelper.DestroyUi(player, UiAdmin);

        #endregion

        #region Helpers

        private bool HasAdmin(BasePlayer player) => player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermAdmin));

        private KitDefinition FindKit(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _config.Киты.Find(k => string.Equals(k.ID, id, StringComparison.OrdinalIgnoreCase));
        }

        private PlayerKitData GetPlayerData(ulong userId)
        {
            if (!_data.Игроки.ContainsKey(userId)) _data.Игроки[userId] = new PlayerKitData();
            return _data.Игроки[userId];
        }

        private ItemContainer GetContainer(BasePlayer player, string container)
        {
            switch ((container ?? string.Empty).ToLower())
            {
                case "belt": return player.inventory.containerBelt;
                case "wear": return player.inventory.containerWear;
                default: return player.inventory.containerMain;
            }
        }

        private string GetNextKitId()
        {
            var index = _config.Киты.Count + 1;
            while (FindKit("kit_" + index) != null) index++;
            return "kit_" + index;
        }

        private double Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}ч {ts.Minutes}м";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}м {ts.Seconds}с";
            return $"{ts.Seconds}с";
        }

        #endregion
    }
}
