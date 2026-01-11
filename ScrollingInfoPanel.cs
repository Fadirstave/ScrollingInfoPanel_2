using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace Oxide.Plugins
{
    [Info("ScrollingInfoPanel", "FadirStave", "1.0.2-baseline")]
    [Description("Stable scrolling info panel with HUD toggle")]
    public class ScrollingInfoPanel : RustPlugin
    {
        private const string UiRoot = "ScrollingInfoPanel.Root";
        private const string ScrollTextA = "ScrollingInfoPanel.ScrollTextA";
        private const string ScrollTextB = "ScrollingInfoPanel.ScrollTextB";

        private const string ChatPrefix =
            "<color=#C46A2A>HUD:</color> ";

        private class ConfigData
        {
            public string TitleText = "THE COMMONWEALTH";
            public List<string> ScrollingMessages = new List<string>();
            public float BackgroundTransparency = 0.6f;
            public string AnchorMin = "0.78 0.92";
            public string AnchorMax = "0.98 0.98";
        }

        private ConfigData config;

        // Session-only hidden players
        private readonly HashSet<ulong> hiddenThisSession = new HashSet<ulong>();

        #region CONFIG

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigData>();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region SCROLL

        private int messageIndex;
        private int scrollIndex;
        private int tickCounter;
        private string cachedScrollLine;
        private readonly Dictionary<ulong, bool> playerBufferToggle = new Dictionary<ulong, bool>();

        private const int VisibleCharacters = 50;
        private const int FadeChars = 4;
        private const float TickRate = 0.14f;
        private const int TicksPerStep = 2;

        private const string CoreTextColor = "3A2A1A";

        private string CurrentScrollLine => cachedScrollLine;

        private void BuildScrollLine()
        {
            if (config.ScrollingMessages == null || config.ScrollingMessages.Count == 0)
            {
                cachedScrollLine = null;
                return;
            }

            var message = config.ScrollingMessages[messageIndex];
            cachedScrollLine = new string(' ', VisibleCharacters) + message + new string(' ', VisibleCharacters);
        }

        #endregion

        #region LIFECYCLE ✅ STABLE

        private void OnPlayerConnected(BasePlayer player)
        {
            timer.Once(2f, () =>
            {
                if (player == null || !player.IsConnected)
                    return;

                if (hiddenThisSession.Contains(player.userID))
                    return;

                DrawUI(player);
            });
        }

        private void OnServerInitialized()
        {
            EnsureDefaultMessages();
            timer.Every(TickRate, UpdateScroll);

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UiRoot);
                playerBufferToggle.Remove(player.userID);
            }
        }

        #endregion

        #region COMMAND ✅ /hud

        [ChatCommand("hud")]
        private void ToggleHud(BasePlayer player, string command, string[] args)
        {
            if (hiddenThisSession.Contains(player.userID))
            {
                // Show HUD
                hiddenThisSession.Remove(player.userID);
                DrawUI(player);
                SendReply(player, $"{ChatPrefix}<color=#FFFFFF>Visible</color>");
            }
            else
            {
                // Hide HUD
                hiddenThisSession.Add(player.userID);
                CuiHelper.DestroyUi(player, UiRoot);
                SendReply(player, $"{ChatPrefix}<color=#FFFFFF>Hidden</color>");
            }
        }

        #endregion

        #region UI

        private void DrawUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiRoot);
            playerBufferToggle[player.userID] = true;
            scrollIndex = 0;
            messageIndex %= config.ScrollingMessages?.Count > 0 ? config.ScrollingMessages.Count : 1;
            BuildScrollLine();

            var c = new CuiElementContainer();
            float a = config.BackgroundTransparency;

            c.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform =
                {
                    AnchorMin = config.AnchorMin,
                    AnchorMax = config.AnchorMax
                }
            }, "Hud", UiRoot);

            c.Add(new CuiPanel
            {
                Image = { Color = $"0.18 0.18 0.18 {a}" },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                    OffsetMin = "2 1",
                    OffsetMax = "-2 -1"
                }
            }, UiRoot, "Frame");

            c.Add(new CuiPanel
            {
                Image = { Color = $"0.478 0.122 0.122 {a}" },
                RectTransform = { AnchorMin = "0 0.6", AnchorMax = "1 1" }
            }, "Frame", "TitleBar");

            c.Add(new CuiLabel
            {
                Text =
                {
                    Text = config.TitleText,
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0.95 0.91 0.85 1"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "TitleBar");

            c.Add(new CuiPanel
            {
                Image = { Color = $"0.85 0.78 0.63 {a}" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.6" }
            }, "Frame", "MessageBar");

            c.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.025 0.2", AnchorMax = "0.975 0.8" }
            }, "MessageBar", "ScrollArea");

            CuiHelper.AddUi(player, c);
            CuiHelper.DestroyUi(player, ScrollTextA);
            CuiHelper.DestroyUi(player, ScrollTextB);
            RefreshScrollText(player);
        }

        private void UpdateScroll()
        {
            if (CurrentScrollLine == null)
                return;

            tickCounter++;
            if (tickCounter < TicksPerStep) return;

            tickCounter = 0;
            scrollIndex++;

            if (scrollIndex > CurrentScrollLine.Length - VisibleCharacters)
            {
                scrollIndex = 0;
                messageIndex = (messageIndex + 1) % config.ScrollingMessages.Count;
                BuildScrollLine();
            }

            foreach (var player in BasePlayer.activePlayerList)
                if (!hiddenThisSession.Contains(player.userID))
                    RefreshScrollText(player);
        }

        private void RefreshScrollText(BasePlayer player)
        {
            if (CurrentScrollLine == null)
                return;

            var c = new CuiElementContainer();
            string chunk = CurrentScrollLine.Substring(scrollIndex, VisibleCharacters);
            var sb = new StringBuilder();

            for (int i = 0; i < chunk.Length; i++)
            {
                int edgeDistance = Mathf.Min(i, chunk.Length - 1 - i);
                if (edgeDistance < FadeChars)
                {
                    float t = (edgeDistance + 1f) / (FadeChars + 1f);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(60f, 255f, t));
                    sb.AppendFormat("<color=#{0}{1:X2}>{2}</color>", CoreTextColor, alpha, chunk[i]);
                }
                else
                {
                    sb.AppendFormat("<color=#{0}FF>{1}</color>", CoreTextColor, chunk[i]);
                }
            }

            bool useBufferA;
            if (!playerBufferToggle.TryGetValue(player.userID, out useBufferA))
            {
                useBufferA = true;
            }

            var targetName = useBufferA ? ScrollTextA : ScrollTextB;
            var oldName = useBufferA ? ScrollTextB : ScrollTextA;
            playerBufferToggle[player.userID] = !useBufferA;

            c.Add(new CuiLabel
            {
                Text =
                {
                    Text = sb.ToString(),
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "ScrollArea", targetName);

            CuiHelper.AddUi(player, c);
            CuiHelper.DestroyUi(player, oldName);
        }

        private void EnsureDefaultMessages()
        {
            if (config.ScrollingMessages != null && config.ScrollingMessages.Count > 0)
                return;

            config.ScrollingMessages = new List<string>
            {
                "WELCOME TO THE COMMONWEALTH",
                "REMEMBER TO BE RESPECTFUL AND HAVE FUN"
            };
            SaveConfig();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            playerBufferToggle.Remove(player.userID);
        }

        #endregion
    }
}