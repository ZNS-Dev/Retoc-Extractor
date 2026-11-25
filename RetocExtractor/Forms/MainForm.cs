using RetocExtractor.RetocExtractor.Codes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RetocExtractor
{
    public partial class MainForm : Form
    {
        private List<AssetContainer> assetContainers = [];
        private Dictionary<string, AssetContainer> assetContainersByKey =
            new(StringComparer.OrdinalIgnoreCase);
        private List<string> visibleAssets = [];
        private string visibleAssetsFolder = string.Empty;
        private static readonly object LazyFolderPlaceholder = new();
        private ListViewItem[] virtualItemCache;
        private int virtualItemCacheStart = -1;
        private int searchRequestVersion;
        private Dictionary<string, long> assetSizeCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly AppSettings appSettings;
        private readonly bool isDesignTime;
        private bool busy;
        private bool loadingProfile;
        private bool profileReady;
        private bool settingsSaveErrorShown;
        private bool updatingFolderTree;
        private Timer paksAutoLoadTimer;
        private Timer aesKeyAutoLoadTimer;
        private Timer searchDebounceTimer;
        private string loadedAssetsSourceFolder = string.Empty;
        private string loadedAssetsAesKey = string.Empty;
        private string selectedContainerKey = string.Empty;
        private string selectedFolderPath = string.Empty;
        private readonly string assetsHeaderBase = string.Empty;
        private readonly List<ExportHistoryRecord> exportHistory = [];
        private readonly List<ExportHistoryRecord> pendingExportHistoryEntries = [];
        private const int MaxExportHistoryEntries = 500;
        internal const string DefaultRetocVersionToken = "UE5_5";
        private const string DefaultUnrealVersionDisplayName = "5.5.4";
        private sealed class UnrealVersionOption
        {
            internal readonly string DisplayName;
            internal readonly string RetocToken;

            internal UnrealVersionOption(string displayName, string retocToken)
            {
                DisplayName = displayName;
                RetocToken = retocToken;
            }
        }

        private static readonly UnrealVersionOption[] UnrealVersionOptions =
        [
            new UnrealVersionOption("5.0.3", "UE5_0"),
            new UnrealVersionOption("5.1.1", "UE5_1"),
            new UnrealVersionOption("5.2.1", "UE5_2"),
            new UnrealVersionOption("5.3.2", "UE5_3"),
            new UnrealVersionOption("5.4.4", "UE5_4"),
            new UnrealVersionOption("5.5.4", "UE5_5"),
            new UnrealVersionOption("5.6.1", "UE5_6"),
            new UnrealVersionOption("5.7.4", "UE5_7")
        ];

        // ================== Auto-Update ====================== //
        private string ExtractJsonValue(string json, string key)
        {
            Match match = Regex.Match(
                json,
                $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : null;
        }

        private Version ExtractVersion(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            Match match = Regex.Match(input, @"\d+(\.\d+)+");
            return match.Success && Version.TryParse(match.Value, out Version parsed)
                ? parsed
                : null;
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                string localText = txt_version != null ? txt_version.Text : "";
                Version localVersion = ExtractVersion(localText);

                if (localVersion == null) return;

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                using HttpClient client = new();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Retoc Extractor");

                string apiUrl = "https://api.github.com/repos/ZNS-Dev/Retoc-Extractor/releases/latest";
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();

                string remoteTag = ExtractJsonValue(json, "tag_name");
                string remoteName = ExtractJsonValue(json, "name");
                string downloadUrl = ExtractExeUrlFromAssets(json);

                Version remoteVersion = ExtractVersion(remoteTag) ?? ExtractVersion(remoteName);

                if (remoteVersion == null) return;

                if (remoteVersion > localVersion)
                {

                    DialogResult dialog = MessageBox.Show(
                        $"A new version is available!\n\n" +
                        $"• Current version: {localVersion}\n" +
                        $"• New version: {remoteVersion}\n\n" +
                        $"Would you like to download it and restart the application?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (dialog == DialogResult.Yes)
                    {
                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            MessageBox.Show("Error: No .exe file was found in the Release.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        await ReplaceAndRestartAsync(client, downloadUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update Error] {ex.Message}");
            }
        }

        private async Task ReplaceAndRestartAsync(HttpClient client, string downloadUrl)
        {
            try
            {
                string currentExePath = Application.ExecutablePath;
                string tempExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_temp.exe");

                byte[] exeData = await client.GetByteArrayAsync(downloadUrl);
                File.WriteAllBytes(tempExePath, exeData);

                string cmdArgs = $"/c timeout /t 2 /nobreak > nul & move /y \"{tempExePath}\" \"{currentExePath}\" & start \"\" \"{currentExePath}\"";

                ProcessStartInfo psi = new()
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };

                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to update application: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ExtractExeUrlFromAssets(string json)
        {
            Match match = Regex.Match(json, @"\""browser_download_url\""\s*:\s*\""([^\""]+\.exe)\""", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        // ======================================================== //

        private static string ToDisplayUnrealVersion(string storedValue)
        {
            if (!string.IsNullOrWhiteSpace(storedValue))
            {
                string value = storedValue.Trim();
                foreach (UnrealVersionOption option in UnrealVersionOptions)
                {
                    if (string.Equals(value, option.RetocToken, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, option.DisplayName, StringComparison.OrdinalIgnoreCase))
                        return option.DisplayName;
                }
            }

            return DefaultUnrealVersionDisplayName;
        }

        private static string ToRetocVersionToken(string displayOrStoredValue)
        {
            if (!string.IsNullOrWhiteSpace(displayOrStoredValue))
            {
                string value = displayOrStoredValue.Trim();
                foreach (UnrealVersionOption option in UnrealVersionOptions)
                {
                    if (string.Equals(value, option.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, option.RetocToken, StringComparison.OrdinalIgnoreCase))
                        return option.RetocToken;
                }
            }

            return DefaultRetocVersionToken;
        }

        private static string[] GetUnrealVersionDisplayNames()
        {
            return [.. UnrealVersionOptions.Select(option => option.DisplayName)];
        }

        private void InitializeUnrealVersionCombo()
        {
            cmbVersion.Items.Clear();
            cmbVersion.Items.AddRange(GetUnrealVersionDisplayNames());
            cmbVersion.SelectedItem = ToDisplayUnrealVersion(DefaultRetocVersionToken);
        }

        private const int MaxParallelContainerScans = 2;
        private const int MaxVirtualCacheEntries = 512;
        private string contextTargetAsset = string.Empty;
        private string contextTargetContainerKey = string.Empty;
        private string contextTargetFolderPath = string.Empty;
        private bool folderContextMenuRequested;
        private Font exportHistoryLinkFont;

        private static readonly Regex PathRegex = new(
            @"(?i)([A-Za-z0-9_.@\-]+(?:/[A-Za-z0-9_.@\-]+)+\.[A-Za-z0-9_]+)",
            RegexOptions.Compiled);
        private static readonly Regex SizedChunkLineRegex = new(
            @"(?<![A-Fa-f0-9])(?<id>[A-Fa-f0-9]{24})(?![A-Fa-f0-9])\s+(?<type>\S+)\s+(?<size>\d+)\s+(?<path>.+?)\s*$",
            RegexOptions.Compiled);
        private static readonly Regex UcasPartitionRegex = new(
            @"^(?<container>.+)_s(?<partition>\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public MainForm(AppSettings settings)
        {
            isDesignTime = false;
            appSettings = settings ?? SettingsStore.Load();
            InitializeComponent();
            InitializeUnrealVersionCombo();

            Icon executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (executableIcon != null)
                Icon = executableIcon;
            assetsHeaderBase = lblAssetsHeader.Text;
            InitializeRuntime();
        }
        private void InitializeRuntime()
        {
            profileReady = false;
            loadingProfile = true;

            var versionToolTip = new ToolTip(components);
            versionToolTip.SetToolTip(
                cmbVersion,
                "Patch release labels are displayed for clarity; retoc receives the matching UE major/minor version.");
            UpdateBrowserHeader();

            btnBrowsePaks.Click += (s, e) => BrowsePaks();
            paksAutoLoadTimer = new Timer(components) { Interval = 650 };
            paksAutoLoadTimer.Tick += PaksAutoLoadTimer_Tick;
            aesKeyAutoLoadTimer = new Timer(components) { Interval = 750 };
            aesKeyAutoLoadTimer.Tick += AesKeyAutoLoadTimer_Tick;
            searchDebounceTimer = new Timer(components) { Interval = 180 };
            searchDebounceTimer.Tick += (s, e) =>
            {
                searchDebounceTimer.Stop();
                ApplySearch();
            };
            txtPaks.TextChanged += TxtPaks_TextChanged;
            txtAesKey.TextChanged += TxtAesKey_TextChanged;
            txtSearch.TextChanged += (s, e) =>
            {
                if (searchDebounceTimer == null) return;
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            };

            while (lstAssets.Columns.Count > 2)
                lstAssets.Columns.RemoveAt(lstAssets.Columns.Count - 1);
            if (lstAssets.Columns.Count == 0)
                lstAssets.Columns.Add(colAssetName);
            if (lstAssets.Columns.Count == 1)
                lstAssets.Columns.Add(colAssetSize);

            // Native painting avoids the hover repaint issue previously seen with OwnerDraw.
            lstAssets.VirtualMode = true;
            lstAssets.OwnerDraw = false;
            lstAssets.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lstAssets.HideSelection = false;
            lstAssets.RetrieveVirtualItem += LstAssets_RetrieveVirtualItem;
            lstAssets.CacheVirtualItems += LstAssets_CacheVirtualItems;
            lstAssets.SizeChanged += (s, e) => UpdateAssetColumns();
            contextMenuExport.Opening += ContextMenuExport_Opening;
            menuExportUassetOnly.Click += async (s, e) => await ExportContextAssetsAsync(ExportVariant.MainPackageOnly);
            menuExportUexpOnly.Click += async (s, e) => await ExportContextAssetsAsync(ExportVariant.UExpOnly);
            menuExportBoth.Click += async (s, e) => await ExportContextAssetsAsync(ExportVariant.Both);
            trvFolders.AfterSelect += TrvFolders_AfterSelect;
            trvFolders.BeforeExpand += TrvFolders_BeforeExpand;
            trvFolders.MouseDown += TrvFolders_MouseDown;
            trvFolders.ShowNodeToolTips = true;
            pnlExportHistory.SizeChanged += (s, e) => UpdateHistoryLinkWidths();
            UpdateAssetColumns();
            lstAssets.MouseDown += LstAssets_MouseDown;
            cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;
            btnNewProfile.Click += (s, e) => CreateProfile();
            btnDeleteProfile.Click += (s, e) => DeleteSelectedProfile();

            txtPaks.Leave += (s, e) => SaveCurrentProfile();
            txtAesKey.Leave += (s, e) => SaveCurrentProfile();
            cmbVersion.SelectedIndexChanged += (s, e) => SaveCurrentProfile();
            FormClosing += (s, e) =>
            {
                System.Threading.Interlocked.Increment(ref searchRequestVersion);
                SaveEverything();
            };
            Shown += MainForm_Shown;

            PopulateProfileCombo();
            if (cmbProfile.Items.Count > 0)
            {
                string rememberedProfileName = appSettings.ActiveProfileName;
                int rememberedIndex = -1;
                for (int i = 0; i < cmbProfile.Items.Count; i++)
                {
                    if (string.Equals(Convert.ToString(cmbProfile.Items[i]), rememberedProfileName, StringComparison.OrdinalIgnoreCase))
                    {
                        rememberedIndex = i;
                        break;
                    }
                }

                cmbProfile.SelectedIndex = rememberedIndex >= 0 ? rememberedIndex : 0;
                appSettings.ActiveProfileName = Convert.ToString(cmbProfile.SelectedItem);
            }

            LoadActiveProfile();
            BuildFolderTree();
            loadingProfile = false;
            profileReady = true;
            SaveCurrentProfile();
            SaveSettingsSafely();

            try
            {
                Directory.CreateDirectory(GetExportDirectory());
                Directory.CreateDirectory(GetDataDirectory());
                ClearExportHistoryLinks();

                string obsoleteHistoryFile = Path.Combine(GetDataDirectory(), "export-history.xml");
                try { if (File.Exists(obsoleteHistoryFile)) File.Delete(obsoleteHistoryFile); }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Startup error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {

            Shown -= MainForm_Shown;
            await TryAutoLoadAssetsAsync();
        }

        private static string GetAssetFileName(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return string.Empty;
            int separator = assetPath.LastIndexOf('/');
            return separator >= 0 ? assetPath.Substring(separator + 1) : assetPath;
        }

        private void LstAssets_MouseDown(object sender, MouseEventArgs e)
        {

            folderContextMenuRequested = false;
            contextTargetContainerKey = string.Empty;
            contextTargetFolderPath = string.Empty;
            contextTargetAsset = string.Empty;
            if (e.Button != MouseButtons.Right)
                return;

            ListViewHitTestInfo hit = lstAssets.HitTest(e.Location);
            if (hit.Item == null)
                return;

            int hitIndex = hit.Item.Index;
            if (hitIndex < 0 || hitIndex >= visibleAssets.Count) return;
            if (!lstAssets.SelectedIndices.Contains(hitIndex))
            {
                lstAssets.SelectedIndices.Clear();
                lstAssets.SelectedIndices.Add(hitIndex);
            }
            lstAssets.Focus();
            contextTargetAsset = GetVisibleAssetPath(hitIndex);
        }

        private void TrvFolders_MouseDown(object sender, MouseEventArgs e)
        {
            folderContextMenuRequested = false;
            contextTargetContainerKey = string.Empty;
            contextTargetFolderPath = string.Empty;
            contextTargetAsset = string.Empty;
            if (e.Button != MouseButtons.Right)
                return;

            TreeNode node = trvFolders.GetNodeAt(e.Location);
            if (node == null)
                return;

            trvFolders.SelectedNode = node;
            if (node.Tag is not FolderNodeInfo nodeInfo || string.IsNullOrWhiteSpace(nodeInfo.FolderPath))
                return;

            contextTargetContainerKey = nodeInfo.ContainerKey;
            contextTargetFolderPath = NormalizeAssetPath(nodeInfo.FolderPath).Trim('/');
            folderContextMenuRequested = contextTargetFolderPath.Length > 0;
        }

        private void TrvFolders_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (updatingFolderTree || e.Node == null) return;
            FolderNodeInfo nodeInfo = e.Node.Tag as FolderNodeInfo;
            selectedContainerKey = nodeInfo == null ? string.Empty : nodeInfo.ContainerKey;
            selectedFolderPath = nodeInfo == null ? string.Empty : nodeInfo.FolderPath;
            UpdateBrowserHeader();
            searchDebounceTimer?.Stop();
            ApplySearch();
        }

        private void BuildFolderTree()
        {
            string desiredContainer = selectedContainerKey ?? string.Empty;
            string desiredFolder = selectedFolderPath ?? string.Empty;
            updatingFolderTree = true;
            trvFolders.BeginUpdate();
            try
            {
                trvFolders.Nodes.Clear();
                TreeNode selectedNode = null;

                foreach (AssetContainer container in assetContainers)
                {
                    var containerNode = new TreeNode(FormatTreeNodeText(container.DisplayName, container.AssetCount))
                    {
                        Tag = new FolderNodeInfo
                        {
                            ContainerKey = container.Key,
                            FolderPath = string.Empty,
                            DisplayName = container.DisplayName
                        },
                        ToolTipText = container.ToolTip + (container.AssetCount > 0
                            ? Environment.NewLine + "Assets: " + container.AssetCount.ToString("N0", CultureInfo.CurrentCulture)
                            : string.Empty)
                    };
                    trvFolders.Nodes.Add(containerNode);

                    AddFolderTreeChildren(containerNode, container.Key, string.Empty);
                    containerNode.Expand();

                    if (selectedNode == null &&
                        string.Equals(container.Key, desiredContainer, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedNode = FindFolderNode(containerNode, desiredFolder);
                    }
                }

                if (selectedNode == null && trvFolders.Nodes.Count > 0)
                {
                    AssetContainer firstPopulated = assetContainers.FirstOrDefault(container => container.PackageAssets.Length > 0);
                    if (firstPopulated != null)
                    {
                        foreach (TreeNode rootNode in trvFolders.Nodes)
                        {
                            if (rootNode.Tag is FolderNodeInfo rootInfo && string.Equals(rootInfo.ContainerKey, firstPopulated.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                selectedNode = rootNode;
                                break;
                            }
                        }
                    }

                    selectedNode ??= trvFolders.Nodes[0];
                }

                trvFolders.SelectedNode = selectedNode;
                FolderNodeInfo selectedInfo = selectedNode == null ? null : selectedNode.Tag as FolderNodeInfo;
                selectedContainerKey = selectedInfo == null ? string.Empty : selectedInfo.ContainerKey;
                selectedFolderPath = selectedInfo == null ? string.Empty : selectedInfo.FolderPath;
            }
            finally
            {
                trvFolders.EndUpdate();
                updatingFolderTree = false;
            }
            UpdateBrowserHeader();
        }

        private void AddFolderTreeChildren(TreeNode parentNode, string containerKey, string parentPath)
        {
            if (!assetContainersByKey.TryGetValue(containerKey ?? string.Empty, out AssetContainer container)) return;

            if (!container.FolderChildren.TryGetValue(parentPath ?? string.Empty, out List<string> childNames)) return;

            foreach (string childName in childNames)
            {
                string childPath = string.IsNullOrEmpty(parentPath) ? childName : parentPath + "/" + childName;
                container.FolderAssetCounts.TryGetValue(childPath, out int assetCount);
                var childNode = new TreeNode(FormatTreeNodeText(childName, assetCount))
                {
                    Tag = new FolderNodeInfo
                    {
                        ContainerKey = containerKey,
                        FolderPath = childPath,
                        DisplayName = childName
                    },
                    ToolTipText = assetCount > 0
                        ? "Assets: " + assetCount.ToString("N0", CultureInfo.CurrentCulture)
                        : string.Empty
                };
                if (container.FolderChildren.ContainsKey(childPath))
                    childNode.Nodes.Add(new TreeNode { Tag = LazyFolderPlaceholder });
                parentNode.Nodes.Add(childNode);
            }
        }

        private void PopulateFolderNode(TreeNode node)
        {
            if (node == null || node.Nodes.Count != 1 ||
                !ReferenceEquals(node.Nodes[0].Tag, LazyFolderPlaceholder))
                return;

            if (node.Tag is not FolderNodeInfo nodeInfo) return;
            node.Nodes.Clear();
            AddFolderTreeChildren(node, nodeInfo.ContainerKey, nodeInfo.FolderPath);
        }

        private void TrvFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e) => PopulateFolderNode(e.Node);

        private TreeNode FindFolderNode(TreeNode root, string path)
        {
            if (root == null) return null;
            if (string.IsNullOrWhiteSpace(path)) return root;

            string[] parts = NormalizeAssetPath(path).Trim('/').Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            TreeNode current = root;
            foreach (string part in parts)
            {
                current.Expand();
                PopulateFolderNode(current);
                TreeNode next = null;
                foreach (TreeNode child in current.Nodes)
                {
                    if (ReferenceEquals(child.Tag, LazyFolderPlaceholder)) continue;
                    if (child.Tag is FolderNodeInfo childInfo &&
                        string.Equals(childInfo.DisplayName, part, StringComparison.OrdinalIgnoreCase))
                    {
                        next = child;
                        break;
                    }
                }
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private static FolderTreeIndex BuildFolderTreeIndex(IList<string> assets)
        {
            var index = new FolderTreeIndex();
            var building = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var folderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (assets == null) return index;

            foreach (string asset in assets)
            {
                if (string.IsNullOrEmpty(asset)) continue;
                if (IsSupportedPackageAsset(asset))
                    packagePaths.Add(asset);

                int directoryEnd = asset.LastIndexOf('/');
                if (directoryEnd <= 0) continue;

                string parent = string.Empty;
                int segmentStart = 0;
                while (segmentStart < directoryEnd)
                {
                    int separator = asset.IndexOf('/', segmentStart, directoryEnd - segmentStart);
                    int segmentEnd = separator >= 0 ? separator : directoryEnd;
                    if (segmentEnd <= segmentStart)
                    {
                        segmentStart = segmentEnd + 1;
                        continue;
                    }

                    string part = asset.Substring(segmentStart, segmentEnd - segmentStart);
                    if (!building.TryGetValue(parent, out HashSet<string> children))
                    {
                        children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        building.Add(parent, children);
                    }
                    children.Add(part);
                    parent = string.IsNullOrEmpty(parent) ? part : parent + "/" + part;
                    folderPaths.Add(parent);
                    segmentStart = segmentEnd + 1;
                }
            }

            // This single sorted index supplies the folder counters and the asset browser.
            string[] packageAssets = [.. packagePaths];
            Array.Sort(packageAssets, StringComparer.OrdinalIgnoreCase);
            index.PackageAssets = packageAssets;

            foreach (string folderPath in folderPaths)
            {
                string prefix = folderPath + "/";
                int first = LowerBoundPackagePath(packageAssets, prefix);
                int last = first;
                while (last < packageAssets.Length &&
                       packageAssets[last].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    last++;
                }

                int recursiveCount = last - first;
                if (recursiveCount > 0)
                {
                    index.FolderAssetRanges[folderPath] = new AssetRange(first, recursiveCount);

                    // Folder labels count direct packages only; descendants have their own nodes.
                    int directCount = 0;
                    for (int i = first; i < last; i++)
                    {

                        if (packageAssets[i].IndexOf('/', prefix.Length) < 0)
                            directCount++;
                    }

                    if (directCount > 0)
                        index.FolderAssetCounts[folderPath] = directCount;
                }
            }

            index.FolderChildren = new Dictionary<string, List<string>>(building.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, HashSet<string>> pair in building)
            {
                var sortedChildren = new List<string>(pair.Value);
                sortedChildren.Sort(StringComparer.OrdinalIgnoreCase);
                index.FolderChildren.Add(pair.Key, sortedChildren);
            }
            return index;
        }

        private static int LowerBoundPackagePath(string[] sortedPaths, string value)
        {
            int low = 0;
            int high = sortedPaths == null ? 0 : sortedPaths.Length;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (StringComparer.OrdinalIgnoreCase.Compare(sortedPaths[middle], value) < 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }

        private static bool IsDirectPackageInFolder(string assetPath, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            string normalizedFolder = NormalizeAssetPath(folderPath ?? string.Empty).Trim('/');
            if (normalizedFolder.Length == 0)
                return true;

            string prefix = normalizedFolder + "/";
            if (!assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            return assetPath.IndexOf('/', prefix.Length) < 0;
        }

        private static AssetRange GetFolderAssetRange(AssetContainer container, string folderPath)
        {
            if (container == null || container.PackageAssets == null || container.PackageAssets.Length == 0)
                return new AssetRange(0, 0);

            string normalizedFolder = NormalizeAssetPath(folderPath ?? string.Empty).Trim('/');
            if (normalizedFolder.Length == 0)
                return new AssetRange(0, container.PackageAssets.Length);

            return container.FolderAssetRanges.TryGetValue(normalizedFolder, out AssetRange range)
                ? range
                : new AssetRange(0, 0);
        }

        private static string FormatTreeNodeText(string name, int assetCount)
        {

            string displayName = name ?? string.Empty;
            return assetCount > 0
                ? displayName + " - " + assetCount.ToString("N0", CultureInfo.CurrentCulture)
                : displayName;
        }

        private void UpdateBrowserHeader()
        {
            if (lblAssetsHeader == null) return;
            string prefix = assetsHeaderBase ?? string.Empty;
            string suffix = assetContainersByKey.TryGetValue(selectedContainerKey ?? string.Empty, out AssetContainer container)
                ? (string.IsNullOrEmpty(selectedFolderPath)
                    ? container.DisplayName
                    : container.DisplayName + "  /  " + selectedFolderPath)
                : "No UCAS files loaded";
#pragma warning disable IDE0059 // Assignation inutile d'une valeur
            if (assetContainersByKey.TryGetValue(selectedContainerKey ?? string.Empty, value: out AssetContainer selected))
            {
                int shownCount = visibleAssets == null ? 0 : visibleAssets.Count;
                suffix += " — " + shownCount.ToString("N0", CultureInfo.CurrentCulture) +
                    (shownCount == 1 ? " result" : " results");
            }
#pragma warning restore IDE0059 // Assignation inutile d'une valeur
            lblAssetsHeader.Text = prefix + "  /  " + suffix;
        }

        private void UpdateAssetColumns()
        {
            if (lstAssets == null || lstAssets.Columns.Count < 2) return;

            const int sizeWidth = 96;
            int availableWidth = Math.Max(320, lstAssets.ClientSize.Width - 2);
            colAssetSize.Width = sizeWidth;
            colAssetName.Width = Math.Max(180, availableWidth - sizeWidth);
        }

        private string GetCachedAssetSizeDisplay(string assetPath)
        {
            return assetSizeCache.TryGetValue(assetPath ?? string.Empty, out long bytes)
                ? FormatAssetSize(bytes)
                : "—";
        }

        private static string FormatAssetSize(long totalBytes)
        {
            if (totalBytes < 0) return "—";
            if (totalBytes < 1024) return totalBytes.ToString(CultureInfo.CurrentCulture) + " B";
            if (totalBytes < 1024L * 1024) return (totalBytes / 1024d).ToString("0.##", CultureInfo.CurrentCulture) + " KB";
            if (totalBytes < 1024L * 1024 * 1024) return (totalBytes / (1024d * 1024)).ToString("0.##", CultureInfo.CurrentCulture) + " MB";
            return (totalBytes / (1024d * 1024 * 1024)).ToString("0.##", CultureInfo.CurrentCulture) + " GB";
        }

        private List<string> GetPackageAssetsInsideFolder(string folderPath, string containerKey)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return [];
            if (!assetContainersByKey.TryGetValue(containerKey ?? string.Empty, out AssetContainer container))
                return [];

            AssetRange range = GetFolderAssetRange(container, folderPath);
            if (range.Count == 0) return [];

            var assets = new List<string>(range.Count);
            int end = range.Start + range.Count;
            for (int index = range.Start; index < end; index++)
                assets.Add(container.PackageAssets[index]);
            return assets;
        }

        private void ClearLoadedAssets()
        {
            System.Threading.Interlocked.Increment(ref searchRequestVersion);
            searchDebounceTimer?.Stop();
            assetContainers.Clear();
            assetContainersByKey.Clear();
            visibleAssets = [];
            visibleAssetsFolder = string.Empty;
            assetSizeCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            virtualItemCache = null;
            virtualItemCacheStart = -1;
            loadedAssetsSourceFolder = string.Empty;
            loadedAssetsAesKey = string.Empty;
            selectedContainerKey = string.Empty;
            selectedFolderPath = string.Empty;
            if (lstAssets.VirtualMode)
            {
                lstAssets.SelectedIndices.Clear();
                lstAssets.VirtualListSize = 0;
            }
            else
            {
                lstAssets.Items.Clear();
            }
            BuildFolderTree();
        }

        private void PopulateProfileCombo()
        {
            loadingProfile = true;
            try
            {
                cmbProfile.Items.Clear();
                foreach (ProfileSettings profile in appSettings.Profiles)
                    cmbProfile.Items.Add(profile.Name);
                string activeName = appSettings.ActiveProfileName;
                if (SettingsStore.FindProfile(appSettings, activeName) == null && appSettings.Profiles.Count > 0)
                {
                    activeName = appSettings.Profiles[0].Name;
                    appSettings.ActiveProfileName = activeName;
                }

                if (cmbProfile.Items.Count > 0)
                {
                    cmbProfile.SelectedItem = activeName;
                    if (cmbProfile.SelectedItem == null)
                    {
                        cmbProfile.SelectedIndex = 0;
                        appSettings.ActiveProfileName = Convert.ToString(cmbProfile.SelectedItem);
                    }
                }
            }
            finally { loadingProfile = false; }
        }

        private void LoadActiveProfile()
        {
            aesKeyAutoLoadTimer?.Stop();
            ProfileSettings profile = SettingsStore.FindProfile(appSettings, appSettings.ActiveProfileName);
            if (profile == null)
            {
                profile = new ProfileSettings { Name = ProfileSettings.DefaultProfileName };
                appSettings.Profiles.Add(profile);
                appSettings.ActiveProfileName = profile.Name;
            }

            loadingProfile = true;
            try
            {
                txtPaks.Text = profile.PaksPath ?? string.Empty;
                txtAesKey.Text = SettingsStore.UnprotectAesKey(profile.ProtectedAesKey);
                string savedVersion = string.IsNullOrWhiteSpace(profile.UnrealVersion)
                    ? DefaultRetocVersionToken
                    : profile.UnrealVersion;
                string displayVersion = ToDisplayUnrealVersion(savedVersion);
                cmbVersion.SelectedItem = displayVersion;
                string retocVersion = ToRetocVersionToken(displayVersion);
                if (!string.Equals(profile.UnrealVersion, retocVersion, StringComparison.OrdinalIgnoreCase))
                {

                    profile.UnrealVersion = retocVersion;
                }
            }
            finally { loadingProfile = false; }
        }

        private void CmbProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loadingProfile || !profileReady || cmbProfile.SelectedItem == null) return;
            string nextName = cmbProfile.SelectedItem.ToString();
            if (string.Equals(nextName, appSettings.ActiveProfileName, StringComparison.OrdinalIgnoreCase)) return;

            UpdateCurrentProfileFromUi();
            appSettings.ActiveProfileName = nextName;
            ClearLoadedAssets();
            txtSearch.Clear();
            LoadActiveProfile();
            SchedulePaksAutoLoad();
            SaveSettingsSafely();
        }

        private void CreateProfile()
        {
            SaveCurrentProfile();
            string name = PromptForProfileName();
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();

            ProfileSettings existing = SettingsStore.FindProfile(appSettings, name);
            if (existing != null)
            {
                MessageBox.Show(this, "Profile already exists.", "Existing profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SelectProfile(existing.Name);
                return;
            }

            var profile = new ProfileSettings
            {
                Name = name,
                PaksPath = string.Empty,
                ProtectedAesKey = string.Empty,
                UnrealVersion = DefaultRetocVersionToken
            };
            appSettings.Profiles.Add(profile);
            appSettings.ActiveProfileName = profile.Name;
            ClearLoadedAssets();
            loadingProfile = true;
            try { txtSearch.Clear(); }
            finally { loadingProfile = false; }
            PopulateProfileCombo();
            LoadActiveProfile();
            SchedulePaksAutoLoad();
            SaveSettingsSafely();
        }

        private void DeleteSelectedProfile()
        {
            if (cmbProfile.SelectedItem == null) return;
            if (appSettings.Profiles.Count <= 1)
            {
                MessageBox.Show(this, "At least one profile must remain.", "Cannot delete profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = cmbProfile.SelectedItem.ToString();
            DialogResult answer = MessageBox.Show(this, $"Delete profile '{name}'?", "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            appSettings.Profiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            appSettings.ActiveProfileName = appSettings.Profiles[0].Name;
            ClearLoadedAssets();
            txtSearch.Clear();
            PopulateProfileCombo();
            LoadActiveProfile();
            SchedulePaksAutoLoad();
            SaveSettingsSafely();
        }

        private void SelectProfile(string name)
        {
            if (cmbProfile.Items.Contains(name)) cmbProfile.SelectedItem = name;
        }

        private string PromptForProfileName()
        {
            using var dialog = new ProfileNameDialog();
            return dialog.ShowDialog(this) == DialogResult.OK
                ? dialog.ProfileName
                : null;
        }

        private bool UpdateCurrentProfileFromUi()
        {
            if (isDesignTime || !profileReady || loadingProfile) return false;

            ProfileSettings profile = SettingsStore.FindProfile(appSettings, appSettings.ActiveProfileName);
            if (profile == null) return false;

            profile.PaksPath = txtPaks.Text.Trim();
            profile.ProtectedAesKey = SettingsStore.ProtectAesKey(txtAesKey.Text.Trim());
            profile.UnrealVersion = ToRetocVersionToken(
                cmbVersion.SelectedItem == null ? DefaultUnrealVersionDisplayName : cmbVersion.SelectedItem.ToString());
            return true;
        }

        private void SaveCurrentProfile()
        {
            if (UpdateCurrentProfileFromUi())
                SaveSettingsSafely();
        }

        private void SaveEverything()
        {
            UpdateCurrentProfileFromUi();

            string selectedName = cmbProfile.SelectedItem == null ? string.Empty : cmbProfile.SelectedItem.ToString();
            if (!string.IsNullOrWhiteSpace(selectedName))
                appSettings.ActiveProfileName = selectedName;

            SaveSettingsSafely();
        }

        private void SaveSettingsSafely()
        {
            if (isDesignTime) return;
            try
            {
                SettingsStore.Save(appSettings);
                settingsSaveErrorShown = false;
            }
            catch (Exception ex)
            {

                if (!settingsSaveErrorShown && !IsDisposed)
                {
                    settingsSaveErrorShown = true;
                    MessageBox.Show(this,
                        $"Could not save settings: {ex.Message}",
                        "Save error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private void TxtPaks_TextChanged(object sender, EventArgs e)
        {
            if (isDesignTime || loadingProfile || !profileReady || paksAutoLoadTimer == null)
                return;

            string requestedFolder = NormalizePaksFolderPath(txtPaks.Text);
            if (!string.IsNullOrEmpty(loadedAssetsSourceFolder) &&
                !string.Equals(requestedFolder, loadedAssetsSourceFolder, StringComparison.OrdinalIgnoreCase))
            {

                ClearLoadedAssets();
            }

            SchedulePaksAutoLoad();
        }

        private void TxtAesKey_TextChanged(object sender, EventArgs e)
        {
            if (isDesignTime || loadingProfile || !profileReady || aesKeyAutoLoadTimer == null)
                return;

            string aesKey = (txtAesKey.Text ?? string.Empty).Trim();
            if (aesKey.Length < 64)
            {
                aesKeyAutoLoadTimer.Stop();
                return;
            }

            aesKeyAutoLoadTimer.Stop();
            aesKeyAutoLoadTimer.Start();
        }

        private void SchedulePaksAutoLoad()
        {
            if (isDesignTime || loadingProfile || !profileReady || paksAutoLoadTimer == null)
                return;

            paksAutoLoadTimer.Stop();
            paksAutoLoadTimer.Start();
        }

        private async void PaksAutoLoadTimer_Tick(object sender, EventArgs e)
        {
            paksAutoLoadTimer.Stop();
            aesKeyAutoLoadTimer?.Stop();
            await TryAutoLoadAssetsAsync();
        }

        private async void AesKeyAutoLoadTimer_Tick(object sender, EventArgs e)
        {
            aesKeyAutoLoadTimer.Stop();
            paksAutoLoadTimer?.Stop();
            if ((txtAesKey.Text ?? string.Empty).Trim().Length < 64)
                return;

            await TryAutoLoadAssetsAsync();
        }

        private async Task TryAutoLoadAssetsAsync()
        {
            if (isDesignTime || loadingProfile || !profileReady || busy)
                return;

            string paks = (txtPaks.Text ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(paks) || !Directory.Exists(paks))
                return;

            string retoc = GetRetocExecutablePath();
            if (!File.Exists(retoc))
                return;

            string normalizedFolder = NormalizePaksFolderPath(paks);
            string aesKey = (txtAesKey.Text ?? string.Empty).Trim();
            if (HasLoadedAssets() &&
                string.Equals(normalizedFolder, loadedAssetsSourceFolder, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(aesKey, loadedAssetsAesKey, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {

                bool hasContainer = await Task.Run(() =>
                    Directory.EnumerateFiles(paks, "*.utoc", SearchOption.TopDirectoryOnly).Any() ||
                    Directory.EnumerateFiles(paks, "*.ucas", SearchOption.TopDirectoryOnly).Any());
                if (!hasContainer)
                    return;
            }
            catch
            {

                return;
            }

            SaveCurrentProfile();
            await LoadAssetsAsync();
        }

        private bool HasLoadedAssets()
        {
            return assetContainers.Any(container => container != null && container.AssetCount > 0);
        }

        private static string NormalizePaksFolderPath(string path)
        {
            string value = (path ?? string.Empty).Trim().Trim('"');
            if (value.Length == 0) return string.Empty;

            try
            {
                string fullPath = Path.GetFullPath(value);
                string root = Path.GetPathRoot(fullPath);
                if (!string.IsNullOrEmpty(root) &&
                    string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                    return fullPath;

                return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private void BrowsePaks()
        {
            string currentPath = NormalizePaksFolderPath(txtPaks.Text);
            string selectedPath;
            try
            {

                selectedPath = NativeFolderPicker.Show(
                    this,
                    "Select the Unreal Paks folder",
                    currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Folder selection failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedPath)) return;

            string previousPath = NormalizePaksFolderPath(txtPaks.Text);
            bool pathChanged = !string.Equals(previousPath, NormalizePaksFolderPath(selectedPath), StringComparison.OrdinalIgnoreCase);
            if (pathChanged)
                txtSearch.Clear();

            txtPaks.Text = selectedPath;
            SaveCurrentProfile();

            if (!pathChanged && !HasLoadedAssets())
                SchedulePaksAutoLoad();
        }

        private async Task LoadAssetsAsync()
        {
            if (busy) return;

            string retoc = GetRetocExecutablePath();
            string paks = (txtPaks.Text ?? string.Empty).Trim().Trim('"');
            if (!File.Exists(retoc))
            {
                MessageBox.Show(this, "retoc.exe is missing from the Data folder. Restart Retoc Extractor to download it again.", "retoc.exe not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Directory.Exists(paks))
            {
                MessageBox.Show(this, "Choose the game's Paks folder.", "Folder not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool hadLoadedAssetsBeforeScan = HasLoadedAssets();
            SetBusy(true);
            try
            {
                ContainerInventory inventory = await Task.Run(() => BuildContainerInventory(paks));
                string[] tocFiles = inventory.TocFiles;
                List<ContainerScanEntry> scanEntries = inventory.Containers;
                if (tocFiles.Length == 0 && scanEntries.Count == 0)
                {
                    string message = inventory.GlobalContainerExcluded
                        ? "The global container was found and is intentionally hidden from the browser."
                        : "No .utoc files were found directly in this folder. Choose the Content\\Paks folder containing the containers.";
                    MessageBox.Show(this, message, "No .utoc files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var containerByToc = new Dictionary<string, ContainerScanEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (ContainerScanEntry entry in scanEntries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.TocPath))
                        containerByToc[entry.TocPath] = entry;
                }

                var sourceChunksById = new Dictionary<string, ChunkListingEntry>(StringComparer.OrdinalIgnoreCase);
                string aesKey = (txtAesKey.Text ?? string.Empty).Trim();
                var mergeLock = new object();
                var failedContainers = new List<string>();
                int nextContainerIndex = -1;
                int workerCount = Math.Min(Math.Min(MaxParallelContainerScans, Math.Max(1, Environment.ProcessorCount)), tocFiles.Length);

                Task[] workers = new Task[workerCount];
                for (int worker = 0; worker < workerCount; worker++)
                {
                    workers[worker] = Task.Run(() =>
                    {
                        while (true)
                        {
                            int index = System.Threading.Interlocked.Increment(ref nextContainerIndex);
                            if (index >= tocFiles.Length) break;

                            string tocPath = tocFiles[index];
                            if (!containerByToc.TryGetValue(tocPath, out ContainerScanEntry scanEntry))
                                continue;

                            ListingResult result;
                            try
                            {
                                result = ListContainer(retoc, tocPath, aesKey);
                            }
                            catch (Exception containerError)
                            {
                                lock (mergeLock)
                                    failedContainers.Add(Path.GetFileName(tocPath) + ": " + containerError.Message);
                                continue;
                            }

                            lock (mergeLock)
                            {
                                foreach (string asset in result.Paths)
                                {
                                    scanEntry.Paths.Add(asset);
                                }

                                foreach (ChunkListingEntry incoming in result.ChunkEntries)
                                {
                                    if (string.IsNullOrWhiteSpace(incoming.ChunkId)) continue;
                                    if (!sourceChunksById.TryGetValue(incoming.ChunkId, out ChunkListingEntry existing))
                                    {
                                        sourceChunksById.Add(incoming.ChunkId, new ChunkListingEntry
                                        {
                                            ChunkId = incoming.ChunkId,
                                            SizeBytes = incoming.SizeBytes,
                                            Path = incoming.Path
                                        });
                                    }
                                    else
                                    {

                                        if (incoming.SizeBytes > existing.SizeBytes) existing.SizeBytes = incoming.SizeBytes;
                                        if (string.IsNullOrWhiteSpace(existing.Path) && !string.IsNullOrWhiteSpace(incoming.Path))
                                            existing.Path = incoming.Path;
                                        sourceChunksById[incoming.ChunkId] = existing;
                                    }
                                }
                            }
                        }
                    });
                }
                await Task.WhenAll(workers);

                Task<List<AssetContainer>> containerBuildTask = Task.Run(() =>
                {
                    var built = new List<AssetContainer>(scanEntries.Count);
                    foreach (ContainerScanEntry entry in scanEntries)
                    {
                        string[] paths = [.. entry.Paths];
                        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

                        // A logical container pairs one .utoc with its base .ucas and _sN partitions.
                        string displayName = Path.GetFileName(entry.UcasPath);
                        int partitionCount = entry.UcasFiles.Count(file =>
                            !string.Equals(file, entry.UcasPath, StringComparison.OrdinalIgnoreCase));
                        if (!entry.UcasExists && entry.UcasFiles.Count == 0)
                        {
                            displayName += " (missing)";
                        }
                        else if (!entry.UcasExists)
                        {
                            displayName += " (base missing; " + partitionCount + " partition" +
                                (partitionCount == 1 ? string.Empty : "s") + ")";
                        }
                        else if (partitionCount > 0)
                        {
                            displayName += " (+" + partitionCount + " partition" +
                                (partitionCount == 1 ? string.Empty : "s") + ")";
                        }

                        if (!entry.HasToc)
                            displayName += " (no .utoc)";

                        string toolTip = entry.UcasFiles.Count == 0
                            ? "Expected UCAS data file was not found: " + entry.UcasPath
                            : "UCAS data files:" + Environment.NewLine +
                              string.Join(Environment.NewLine, entry.UcasFiles.OrderBy(file => file, StringComparer.OrdinalIgnoreCase));
                        if (entry.HasToc)
                            toolTip += Environment.NewLine + ".utoc index:" + Environment.NewLine + entry.TocPath;
                        else
                            toolTip += Environment.NewLine + "No matching .utoc index was found.";

                        FolderTreeIndex folderIndex = BuildFolderTreeIndex(paths);
                        built.Add(new AssetContainer
                        {
                            Key = entry.Key,
                            DisplayName = displayName,
                            ToolTip = toolTip,
                            PackageAssets = folderIndex.PackageAssets,
                            AssetCount = folderIndex.PackageAssets.Length,
                            FolderChildren = folderIndex.FolderChildren,
                            FolderAssetCounts = folderIndex.FolderAssetCounts,
                            FolderAssetRanges = folderIndex.FolderAssetRanges
                        });
                    }

                    built.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName));
                    return built;
                });
                Task<Dictionary<string, long>> sizeIndexTask =
                    Task.Run(() => BuildSourceAssetSizes(sourceChunksById.Values));
                await Task.WhenAll(containerBuildTask, sizeIndexTask);

                List<AssetContainer> rebuiltContainers = containerBuildTask.Result;
                bool hasNewAssets = rebuiltContainers.Any(container => container != null && container.AssetCount > 0);
                if (hadLoadedAssetsBeforeScan && !hasNewAssets)
                {
                    MessageBox.Show(this,
                        "No paths could be read with this AES key. The previously loaded assets were kept. Check the AES key and try again.",
                        "AES key not accepted",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                assetContainers = rebuiltContainers;
                assetContainersByKey = assetContainers.ToDictionary(container => container.Key, StringComparer.OrdinalIgnoreCase);
                assetSizeCache = sizeIndexTask.Result;
                sourceChunksById.Clear();
                sourceChunksById = null;
                BuildFolderTree();
                ApplySearch();
                loadedAssetsSourceFolder = NormalizePaksFolderPath(paks);
                loadedAssetsAesKey = aesKey;

                if (tocFiles.Length == 0)
                {
                    MessageBox.Show(this,
                        "UCAS files were found, but no matching .utoc indexes were found. The files are shown in the container tree, but their assets cannot be listed.",
                        "No .utoc files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (!HasLoadedAssets())
                {
                    MessageBox.Show(this, "No paths could be read. Check the retoc version and, if needed, the AES key.", "Empty list", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                if (failedContainers.Count > 0)
                {
                    string details = string.Join(Environment.NewLine, failedContainers.Take(4));
                    if (failedContainers.Count > 4)
                        details += Environment.NewLine + "…";
                    MessageBox.Show(this,
                        "Some .utoc containers could not be read (" + failedContainers.Count + ")." +
                        Environment.NewLine + details,
                        "Partial Paks scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "retoc error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static ContainerInventory BuildContainerInventory(string paksFolder)
        {
            string[] ucasFiles = Directory.GetFiles(paksFolder, "*.ucas", SearchOption.TopDirectoryOnly);
            string[] allTocFiles = Directory.GetFiles(paksFolder, "*.utoc", SearchOption.TopDirectoryOnly);
            bool globalContainerExcluded = ucasFiles.Any(IsGlobalContainerOrPartitionPath) ||
                                           allTocFiles.Any(IsGlobalContainerPath);
            string[] tocFiles = [.. allTocFiles.Where(path => !IsGlobalContainerPath(path))];
            Array.Sort(ucasFiles, StringComparer.OrdinalIgnoreCase);
            Array.Sort(tocFiles, StringComparer.OrdinalIgnoreCase);

            var byPath = new Dictionary<string, ContainerScanEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (string ucasPath in ucasFiles)
            {
                string fullPath = Path.GetFullPath(ucasPath);
                string fileStem = Path.GetFileNameWithoutExtension(fullPath);
                bool hasExactToc = File.Exists(Path.ChangeExtension(fullPath, ".utoc"));
                Match partitionMatch = hasExactToc
                    ? UcasPartitionRegex.Match(string.Empty)
                    : UcasPartitionRegex.Match(fileStem);
                string logicalUcasPath = partitionMatch.Success
                    ? Path.Combine(Path.GetDirectoryName(fullPath), partitionMatch.Groups["container"].Value + ".ucas")
                    : fullPath;
                logicalUcasPath = Path.GetFullPath(logicalUcasPath);

                if (!byPath.TryGetValue(logicalUcasPath, out ContainerScanEntry entry))
                {
                    entry = new ContainerScanEntry
                    {
                        Key = logicalUcasPath,
                        UcasPath = logicalUcasPath,
                        Paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    };
                    byPath.Add(logicalUcasPath, entry);
                }

                entry.UcasFiles.Add(fullPath);
                if (!partitionMatch.Success)
                    entry.UcasExists = true;
            }

            foreach (string tocPath in tocFiles)
            {
                string fullTocPath = Path.GetFullPath(tocPath);
                string expectedUcasPath = Path.GetFullPath(Path.ChangeExtension(fullTocPath, ".ucas"));
                if (!byPath.TryGetValue(expectedUcasPath, out ContainerScanEntry entry))
                {
                    entry = new ContainerScanEntry
                    {
                        Key = expectedUcasPath,
                        UcasPath = expectedUcasPath,
                        UcasExists = false,
                        Paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    };
                    byPath.Add(expectedUcasPath, entry);
                }

                if (File.Exists(expectedUcasPath))
                {
                    entry.UcasExists = true;
                    if (!entry.UcasFiles.Contains(expectedUcasPath, StringComparer.OrdinalIgnoreCase))
                        entry.UcasFiles.Add(expectedUcasPath);
                }

                entry.TocPath = fullTocPath;
                entry.HasToc = true;
            }

            var containers = new List<ContainerScanEntry>(byPath.Values.Where(entry =>
                !IsGlobalContainerPath(entry.UcasPath)));
            containers.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileName(left.UcasPath), Path.GetFileName(right.UcasPath)));

            return new ContainerInventory
            {
                TocFiles = tocFiles,
                Containers = containers,
                GlobalContainerExcluded = globalContainerExcluded
            };
        }

        private static bool IsGlobalContainerPath(string path)
        {
            return string.Equals(
                Path.GetFileNameWithoutExtension(path ?? string.Empty),
                "global",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGlobalContainerOrPartitionPath(string path)
        {
            if (IsGlobalContainerPath(path)) return true;
            string fileStem = Path.GetFileNameWithoutExtension(path ?? string.Empty);
            Match match = UcasPartitionRegex.Match(fileStem ?? string.Empty);
            return match.Success && string.Equals(
                match.Groups["container"].Value,
                "global",
                StringComparison.OrdinalIgnoreCase);
        }

        private static ListingResult RunListingProcess(string executable, string[] args)
        {
            var start = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = string.Join(" ", args.Select(QuoteArgument)),
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };

            using var process = new Process { StartInfo = start };
            process.Start();
            Task<ListingResult> stdoutTask = Task.Factory.StartNew(
                () => ParseListingStream(process.StandardOutput),
                System.Threading.CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            Task<ListingResult> stderrTask = Task.Factory.StartNew(
                () => ParseListingStream(process.StandardError),
                System.Threading.CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            if (!process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds))
            {
                try { process.Kill(); } catch { }
                process.WaitForExit();
                try { Task.WaitAll(stdoutTask, stderrTask); } catch { }
                throw new TimeoutException("retoc did not finish within 5 minutes.");
            }

            process.WaitForExit();
            Task.WaitAll(stdoutTask, stderrTask);
            ListingResult merged = stdoutTask.Result;
            ListingResult errors = stderrTask.Result;
            merged.Paths.UnionWith(errors.Paths);
            merged.ChunkEntries.AddRange(errors.ChunkEntries);
            return merged;
        }

        private static ListingResult ParseListingStream(TextReader reader)
        {
            var pathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var chunkEntries = new List<ChunkListingEntry>();
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                foreach (Match pathMatch in PathRegex.Matches(line))
                {
                    string raw = pathMatch.Groups[1].Value.Trim().Trim('"', '\'', ',', ';', ')', ']', '`');
                    string path = NormalizeAssetPath(raw);
                    if (!IsVisibleAssetPath(path)) continue;
                    pathSet.Add(path);
                }

                Match sizeMatch = SizedChunkLineRegex.Match(line);
                if (sizeMatch.Success)
                {
                    if (long.TryParse(sizeMatch.Groups["size"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long size) && size >= 0)
                    {
                        string path = sizeMatch.Groups["path"].Value.Trim().Trim('"', '\'', ',', ';', ')', ']', '`');
                        if (path == "-") path = string.Empty;
                        else path = NormalizeAssetPath(path);

                        chunkEntries.Add(new ChunkListingEntry
                        {
                            ChunkId = sizeMatch.Groups["id"].Value.ToUpperInvariant(),
                            SizeBytes = size,
                            Path = path
                        });
                    }
                }
            }

            return new ListingResult
            {
                Paths = pathSet,
                ChunkEntries = chunkEntries
            };
        }

        private static bool IsVisibleAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length < 4 || path.IndexOf('/') < 0)
                return false;
            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension) ||
                extension.Equals(".ubulk", StringComparison.OrdinalIgnoreCase))
                return false;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return false;
            return !path.StartsWith("Engine/", StringComparison.OrdinalIgnoreCase) &&
                   path.IndexOf("/Engine/", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static ListingResult ListContainer(string retocPath, string tocPath, string aesKey)
        {
            var commands = new List<string[]>();

            var withSize = new List<string>();
            if (!string.IsNullOrWhiteSpace(aesKey)) { withSize.Add("--aes-key"); withSize.Add(aesKey); }
            withSize.Add("list"); withSize.Add(tocPath); withSize.Add("--size"); withSize.Add("--path");
            commands.Add([.. withSize]);

            var withPath = new List<string>();
            if (!string.IsNullOrWhiteSpace(aesKey)) { withPath.Add("--aes-key"); withPath.Add(aesKey); }
            withPath.Add("list"); withPath.Add(tocPath); withPath.Add("--path");
            commands.Add([.. withPath]);

            var basic = new List<string>();
            if (!string.IsNullOrWhiteSpace(aesKey)) { basic.Add("--aes-key"); basic.Add(aesKey); }
            basic.Add("list"); basic.Add(tocPath);
            commands.Add([.. basic]);

            ListingResult bestListing = null;
            Exception lastError = null;
            for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
            {
                string[] args = commands[commandIndex];
                ListingResult listing;
                try
                {
                    listing = RunListingProcess(retocPath, args);
                }
                catch (Exception commandError)
                {

                    lastError = commandError;
                    continue;
                }

                if (listing.Paths.Count == 0) continue;

                List<ChunkListingEntry> entries = listing.ChunkEntries;
                if (bestListing == null || listing.ChunkEntries.Count > bestListing.ChunkEntries.Count ||
                    (listing.ChunkEntries.Count == bestListing.ChunkEntries.Count && listing.Paths.Count > bestListing.Paths.Count))
                    bestListing = listing;

                if (entries.Count > 0) return listing;
                if (commandIndex >= 1) return bestListing;
            }

            if (bestListing != null) return bestListing;
            if (lastError != null) throw lastError;

            return new ListingResult
            {
                Paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                ChunkEntries = []
            };
        }

        private static Dictionary<string, long> BuildSourceAssetSizes(IEnumerable<ChunkListingEntry> entries)
        {
            var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (entries == null) return result;

            // Hide the logical global container together with its unindexed global_sN partitions.
            var packageInfoById = new Dictionary<ulong, PackageChunkSize>();

            foreach (ChunkListingEntry entry in entries)
            {
                if (!TryGetPackageId(entry.ChunkId, out ulong packageId)) continue;

                packageInfoById.TryGetValue(packageId, out PackageChunkSize packageInfo);
                packageInfo.SizeBytes = packageInfo.SizeBytes > long.MaxValue - entry.SizeBytes
                    ? long.MaxValue
                    : packageInfo.SizeBytes + entry.SizeBytes;

                string path = NormalizeAssetPath(entry.Path);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (IsSupportedPackageAsset(path))
                    {
                        packageInfo.PackagePath = path;
                    }
                    else
                    {
                        result.TryGetValue(path, out long existing);
                        result[path] = existing > long.MaxValue - entry.SizeBytes
                            ? long.MaxValue
                            : existing + entry.SizeBytes;
                    }
                }

                packageInfoById[packageId] = packageInfo;
            }

            foreach (KeyValuePair<ulong, PackageChunkSize> package in packageInfoById)
            {
                string path = package.Value.PackagePath;
                if (string.IsNullOrWhiteSpace(path)) continue;

                if (!result.TryGetValue(path, out long existing) || package.Value.SizeBytes > existing)
                    result[path] = package.Value.SizeBytes;
            }

            return result;
        }

        private static bool TryGetPackageId(string chunkId, out ulong packageId)
        {
            packageId = 0;
            if (string.IsNullOrEmpty(chunkId) || chunkId.Length < 16) return false;

            for (int i = 0; i < 16; i++)
            {
                char value = chunkId[i];
                int digit;
                if (value >= '0' && value <= '9') digit = value - '0';
                else if (value >= 'a' && value <= 'f') digit = value - 'a' + 10;
                else if (value >= 'A' && value <= 'F') digit = value - 'A' + 10;
                else return false;

                packageId = (packageId << 4) | (uint)digit;
            }
            return true;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            path = path.Trim().Trim('"', '\'', ',', ';', ')', ']', '`').Replace('\\', '/');

            while (path.StartsWith("../", StringComparison.Ordinal)) path = path.Substring(3);
            while (path.StartsWith("./", StringComparison.Ordinal)) path = path.Substring(2);
            path = path.TrimStart('/');
            return path;
        }

        private void LstAssets_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= visibleAssets.Count)
            {
                e.Item = new ListViewItem(string.Empty);
                return;
            }

            if (virtualItemCache != null && e.ItemIndex >= virtualItemCacheStart &&
                e.ItemIndex < virtualItemCacheStart + virtualItemCache.Length)
            {
                e.Item = virtualItemCache[e.ItemIndex - virtualItemCacheStart];
                return;
            }

            e.Item = CreateAssetListViewItem(visibleAssets[e.ItemIndex]);
        }

        private void LstAssets_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            if (visibleAssets.Count == 0 || e.StartIndex < 0 || e.StartIndex >= visibleAssets.Count)
            {
                virtualItemCache = null;
                virtualItemCacheStart = -1;
                return;
            }

            int start = e.StartIndex;

            int end = Math.Min(Math.Min(e.EndIndex, visibleAssets.Count - 1), start + MaxVirtualCacheEntries - 1);
            if (end < start) return;

            var cache = new ListViewItem[end - start + 1];
            for (int index = start; index <= end; index++)
                cache[index - start] = CreateAssetListViewItem(visibleAssets[index]);

            virtualItemCache = cache;
            virtualItemCacheStart = start;
        }

        private ListViewItem CreateAssetListViewItem(string assetPath)
        {

            string displayName = GetAssetFileName(assetPath);
            string folder = visibleAssetsFolder;
            if (!string.IsNullOrEmpty(folder))
            {
                string prefix = folder + "/";
                if (assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    displayName = assetPath.Substring(prefix.Length);
            }

            var item = new ListViewItem(displayName)
            {
                Tag = assetPath,
                ToolTipText = assetPath
            };
            item.SubItems.Add(GetCachedAssetSizeDisplay(assetPath));
            return item;
        }

        private string GetVisibleAssetPath(int index)
        {
            return index >= 0 && index < visibleAssets.Count ? visibleAssets[index] : string.Empty;
        }

        private async void ApplySearch()
        {
            if (lstAssets == null || txtSearch == null) return;

            int requestVersion = System.Threading.Interlocked.Increment(ref searchRequestVersion);
            string query = (txtSearch.Text ?? string.Empty).Trim();
            string folder = selectedFolderPath ?? string.Empty;
            assetContainersByKey.TryGetValue(selectedContainerKey ?? string.Empty, out AssetContainer selectedContainer);
            string[] sourceAssets = selectedContainer == null
                ? []
                : selectedContainer.PackageAssets;
            AssetRange selectedRange = GetFolderAssetRange(selectedContainer, folder);
            int rangeStart = selectedRange.Start;
            int rangeEnd = selectedRange.Start + selectedRange.Count;

            try
            {
                List<string> filtered = await Task.Run(() =>
                {
                    var matches = new List<string>(selectedRange.Count);
                    for (int i = rangeStart; i < rangeEnd; i++)
                    {
                        if ((i & 2047) == 0 &&
                            requestVersion != System.Threading.Volatile.Read(ref searchRequestVersion))
                            return null;

                        string asset = sourceAssets[i];
                        if (!IsDirectPackageInFolder(asset, folder))
                            continue;
                        if (query.Length > 0 && asset.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        matches.Add(asset);
                    }
                    return matches;
                });
                if (filtered == null || IsDisposed || Disposing ||
                    requestVersion != System.Threading.Volatile.Read(ref searchRequestVersion))
                    return;

                lstAssets.BeginUpdate();
                try
                {
                    lstAssets.SelectedIndices.Clear();
                    lstAssets.VirtualListSize = 0;
                    visibleAssets = filtered;
                    visibleAssetsFolder = folder;
                    virtualItemCache = null;
                    virtualItemCacheStart = -1;
                    lstAssets.VirtualListSize = visibleAssets.Count;
                }
                finally
                {
                    lstAssets.EndUpdate();
                }

                if (visibleAssets.Count > 0)
                {
                    try { lstAssets.EnsureVisible(0); }
                    catch (ArgumentOutOfRangeException) { }
                }
                UpdateBrowserHeader();
                UpdateAssetColumns();
                lstAssets.Invalidate();
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing &&
                    requestVersion == System.Threading.Volatile.Read(ref searchRequestVersion))
                {
                    MessageBox.Show(this, ex.Message, "Search error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ContextMenuExport_Opening(object sender, CancelEventArgs e)
        {
            menuExportBoth.Text = "Export Raw data (.uasset + .uexp)";
            menuExportUassetOnly.Text = "Export only .uasset";
            menuExportUexpOnly.Text = "Export only .uexp";

            if (contextMenuExport.SourceControl == trvFolders)
            {
                if (!folderContextMenuRequested)
                {
                    e.Cancel = true;
                    return;
                }

                bool canExportFolder = !busy &&
                    assetContainersByKey.TryGetValue(contextTargetContainerKey ?? string.Empty, out AssetContainer folderContainer) &&
                    GetFolderAssetRange(folderContainer, contextTargetFolderPath).Count > 0;
                menuExportUassetOnly.Enabled = canExportFolder;
                menuExportUexpOnly.Enabled = canExportFolder;
                menuExportBoth.Enabled = canExportFolder;
                if (!canExportFolder) e.Cancel = true;
                return;
            }

            folderContextMenuRequested = false;
            contextTargetContainerKey = string.Empty;
            contextTargetFolderPath = string.Empty;

            if (!IsSupportedPackageAsset(contextTargetAsset))
            {
                Point cursor = lstAssets.PointToClient(Cursor.Position);
                ListViewHitTestInfo hit = lstAssets.HitTest(cursor);
                if (hit.Item != null)
                    contextTargetAsset = GetVisibleAssetPath(hit.Item.Index);
                else if (lstAssets.SelectedIndices.Count > 0)
                    contextTargetAsset = GetVisibleAssetPath(lstAssets.SelectedIndices[0]);
            }

            bool canExport = !busy && IsSupportedPackageAsset(contextTargetAsset);
            menuExportUassetOnly.Enabled = canExport;
            menuExportUexpOnly.Enabled = canExport;
            menuExportBoth.Enabled = canExport;
            if (!canExport) e.Cancel = true;
        }

        private static bool IsSupportedPackageAsset(string assetPath)
        {
            string extension = Path.GetExtension(assetPath ?? string.Empty);
            return string.Equals(extension, ".uasset", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".umap", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ExportContextAssetsAsync(ExportVariant variant)
        {
            if (busy) return;

            if (folderContextMenuRequested)
            {
                await ExportFolderAssetsAsync(contextTargetFolderPath, contextTargetContainerKey, variant);
                return;
            }

            var selectedAssets = new List<string>();
            foreach (int selectedIndex in lstAssets.SelectedIndices)
            {
                string selectedAsset = GetVisibleAssetPath(selectedIndex);
                if (IsSupportedPackageAsset(selectedAsset))
                    selectedAssets.Add(selectedAsset);
            }
            selectedAssets = [.. selectedAssets.Distinct(StringComparer.OrdinalIgnoreCase)];

            bool targetIsSelected = selectedAssets.Any(asset =>
                string.Equals(asset, contextTargetAsset, StringComparison.OrdinalIgnoreCase));

            List<string> assetsToExport = targetIsSelected && selectedAssets.Count > 0
                ? selectedAssets
                : (IsSupportedPackageAsset(contextTargetAsset)
                    ? new List<string> { contextTargetAsset }
                    : []);

            if (assetsToExport.Count == 0) return;

            int succeeded = 0;
            int failed = 0;
            SetBusy(true);
            try
            {
                for (int index = 0; index < assetsToExport.Count; index++)
                {
                    string asset = assetsToExport[index];
                    if (await ExportAssetAsync(asset, variant))
                        succeeded++;
                    else
                        failed++;
                }

                if (failed > 0)
                    MessageBox.Show(this, $"Selected asset export finished. Succeeded: {succeeded}; failed: {failed}.", "Export finished", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                FlushExportHistoryEntries();
                SetBusy(false);
            }
        }

        private async Task ExportFolderAssetsAsync(string folderPath, string containerKey, ExportVariant variant)
        {
            if (busy) return;

            string normalizedFolder = NormalizeAssetPath(folderPath ?? string.Empty).Trim('/');
            List<string> packageAssets = GetPackageAssetsInsideFolder(normalizedFolder, containerKey);
            if (string.IsNullOrWhiteSpace(normalizedFolder) || packageAssets.Count == 0)
            {
                return;
            }

            string retoc = GetRetocExecutablePath();
            string paks = (txtPaks.Text ?? string.Empty).Trim().Trim('"');
            if (!File.Exists(retoc) || !Directory.Exists(paks))
            {
                MessageBox.Show(this, "retoc.exe in Data and the game Paks folder are required before exporting.", "Export unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string output = GetExportDirectory();
            string staging = Path.Combine(Path.GetTempPath(), "RetocExtractor_FolderExport_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(output);
                Directory.CreateDirectory(staging);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Folder export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                return;
            }

            string version = ToRetocVersionToken(cmbVersion.SelectedItem == null ? DefaultUnrealVersionDisplayName : cmbVersion.SelectedItem.ToString());
            string aes = (txtAesKey.Text ?? string.Empty).Trim();
            SetBusy(true);
            try
            {

                string[] args = BuildToLegacyArguments(paks, aes, normalizedFolder, version, staging);
                int exitCode = await Task.Run(() => RunProcess(retoc, args));
                if (exitCode != 0)
                {
                    MessageBox.Show(this, $"retoc failed to export the folder {normalizedFolder}.", "Folder export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string[] allowedExtensions = variant == ExportVariant.MainPackageOnly
                    ? [".uasset", ".umap"]
                    : variant == ExportVariant.UExpOnly
                        ? [".uexp"]
                        : [".uasset", ".umap", ".uexp"];

                int copiedFiles = 0;
                foreach (string sourceFile in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(sourceFile);
                    if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        continue;

                    string relativePath = GetRelativeFilePath(staging, sourceFile);
                    string destinationFile = Path.GetFullPath(Path.Combine(output, relativePath));
                    string outputRoot = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
                    if (!destinationFile.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string destinationDirectory = Path.GetDirectoryName(destinationFile);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory))
                        Directory.CreateDirectory(destinationDirectory);
                    File.Copy(sourceFile, destinationFile, true);
                    copiedFiles++;
                }

                if (copiedFiles == 0)
                {
                    MessageBox.Show(this, $"retoc did not generate any files for folder {normalizedFolder}.", "No files exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                foreach (string asset in packageAssets)
                {
                    string mainPath = GetExpectedExportPath(output, asset);
                    string uexpPath = Path.ChangeExtension(mainPath, ".uexp");
                    bool exists = variant == ExportVariant.MainPackageOnly
                        ? File.Exists(mainPath)
                        : variant == ExportVariant.UExpOnly
                            ? File.Exists(uexpPath)
                            : File.Exists(mainPath) || File.Exists(uexpPath);
                    if (exists) AddExportHistoryEntry(asset, output);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Folder export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                FlushExportHistoryEntries();
                SetBusy(false);
                folderContextMenuRequested = false;
                contextTargetContainerKey = string.Empty;
                contextTargetFolderPath = string.Empty;
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            }
        }

        private static string[] BuildToLegacyArguments(
            string paksFolder, string aesKey, string filter, string version, string stagingDirectory)
        {
            var arguments = new List<string>(10);
            if (!string.IsNullOrWhiteSpace(aesKey))
            {
                arguments.Add("--aes-key");
                arguments.Add(aesKey);
            }

            arguments.Add("to-legacy");
            arguments.Add("--no-shaders");
            arguments.Add("--no-script-objects");
            arguments.Add(paksFolder);
            arguments.Add("--filter");
            arguments.Add(filter);
            arguments.Add("--version");
            arguments.Add(version);
            arguments.Add(stagingDirectory);
            return [.. arguments];
        }

        private enum ExportVariant
        {
            MainPackageOnly,
            UExpOnly,
            Both
        }

        private async Task<bool> ExportAssetAsync(string assetPath, ExportVariant variant)
        {
            if (!IsSupportedPackageAsset(assetPath))
                return false;

            string mainExtension = Path.GetExtension(assetPath);

            string retoc = GetRetocExecutablePath();
            string paks = (txtPaks.Text ?? string.Empty).Trim().Trim('"');
            if (!File.Exists(retoc) || !Directory.Exists(paks))
                return false;

            string output = GetExportDirectory();
            string staging = Path.Combine(Path.GetTempPath(), "RetocExtractor_Export_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(output);
                Directory.CreateDirectory(staging);
            }
            catch
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                return false;
            }

            string version = ToRetocVersionToken(cmbVersion.SelectedItem == null ? DefaultUnrealVersionDisplayName : cmbVersion.SelectedItem.ToString());
            string aes = (txtAesKey.Text ?? string.Empty).Trim();
            bool recordHistory = false;
            try
            {
                string[] args = BuildToLegacyArguments(
                    paks, aes, NormalizeAssetPath(assetPath), version, staging);
                int exitCode = await Task.Run(() => RunProcess(retoc, args));
                if (exitCode != 0)
                {
                    return false;
                }

                string[] requiredExtensions = variant == ExportVariant.MainPackageOnly
                    ? [mainExtension]
                    : variant == ExportVariant.UExpOnly
                        ? [".uexp"]
                        : [mainExtension, ".uexp"];

                var copiedExtensions = new List<string>();
                string outputRoot = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                foreach (string extension in requiredExtensions)
                {
                    string sourceFile = FindStagedAssetFile(staging, assetPath, extension);
                    if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile)) continue;

                    string relativePath = GetRelativeFilePath(staging, sourceFile);
                    string destinationFile = Path.GetFullPath(Path.Combine(output, relativePath));
                    if (!destinationFile.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string destinationDirectory = Path.GetDirectoryName(destinationFile);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);
                    File.Copy(sourceFile, destinationFile, true);
                    copiedExtensions.Add(extension);
                }

                if (copiedExtensions.Count == 0)
                {
                    return false;
                }

                recordHistory = true;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (recordHistory)
                    AddExportHistoryEntry(assetPath, output);
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            }
        }

        private static string FindStagedAssetFile(string stagingRoot, string assetPath, string extension)
        {
            string normalized = NormalizeAssetPath(assetPath).Replace('/', Path.DirectorySeparatorChar);
            string expected = Path.ChangeExtension(Path.Combine(stagingRoot, normalized), extension);
            if (File.Exists(expected)) return expected;

            string fileName = Path.GetFileNameWithoutExtension(assetPath) + extension;
            try
            {
                string[] matches = [.. Directory.EnumerateFiles(stagingRoot, fileName, SearchOption.AllDirectories)];
                if (matches.Length == 1) return matches[0];

                string suffix = Path.DirectorySeparatorChar + normalized;
                string[] pathMatches = [.. matches.Where(path =>
                    Path.GetFullPath(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFullPath(path).EndsWith(Path.ChangeExtension(suffix, extension), StringComparison.OrdinalIgnoreCase))];
                return pathMatches.Length == 1 ? pathMatches[0] : null;
            }
            catch { return null; }
        }

        private static string GetRelativeFilePath(string rootDirectory, string filePath)
        {
            string root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullFile = Path.GetFullPath(filePath);
            if (fullFile.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return fullFile.Substring(root.Length);
            return Path.GetFileName(fullFile);
        }

        private static string GetExportDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
        }

        private static string GetDataDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        }

        private static string GetRetocExecutablePath()
        {
            return Path.Combine(GetDataDirectory(), "retoc.exe");
        }

        private void AddExportHistoryEntry(string assetPath, string outputDirectory)
        {
            var entry = new ExportHistoryRecord
            {
                AssetPath = assetPath ?? string.Empty,
                OutputDirectory = outputDirectory ?? GetExportDirectory()
            };

            exportHistory.Add(entry);
            pendingExportHistoryEntries.Add(entry);
            if (exportHistory.Count > MaxExportHistoryEntries)
            {
                int removeCount = exportHistory.Count - MaxExportHistoryEntries;
                exportHistory.RemoveRange(0, removeCount);
                pendingExportHistoryEntries.RemoveAll(item => !exportHistory.Contains(item));
            }
        }

        private void FlushExportHistoryEntries()
        {
            if (pendingExportHistoryEntries.Count == 0 || pnlExportHistory == null || pnlExportHistory.IsDisposed)
                return;

            pnlExportHistory.SuspendLayout();
            try
            {
                for (int i = 0; i < pendingExportHistoryEntries.Count; i++)
                {
                    ExportHistoryRecord entry = pendingExportHistoryEntries[i];
                    if (!exportHistory.Contains(entry)) continue;

                    string assetLocation = GetHistoryDisplayPath(entry.AssetPath);
                    exportHistoryLinkFont ??= new Font(Font.FontFamily, 8.25f, FontStyle.Bold, GraphicsUnit.Point);
                    int linkWidth = Math.Max(80, pnlExportHistory.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
                    var link = new LinkLabel
                    {
                        Text = assetLocation,
                        Tag = entry,
                        AutoSize = false,
                        AutoEllipsis = true,
                        Height = 19,
                        Width = linkWidth,
                        Margin = new Padding(0),
                        Padding = new Padding(0),
                        TextAlign = ContentAlignment.MiddleLeft,
                        LinkBehavior = LinkBehavior.AlwaysUnderline,
                        UseMnemonic = false,
                        TabStop = true,
                        Cursor = Cursors.Hand,
                        Font = exportHistoryLinkFont,
                    };
                    if (link.Text.Length > 0)
                        link.Links.Add(0, link.Text.Length);
                    link.AccessibleName = assetLocation;
                    link.AccessibleDescription = assetLocation;
                    link.LinkClicked += ExportHistoryLink_LinkClicked;
                    pnlExportHistory.Controls.Add(link);

                    pnlExportHistory.Controls.SetChildIndex(link, 0);
                }
                pendingExportHistoryEntries.Clear();

                while (pnlExportHistory.Controls.Count > MaxExportHistoryEntries)
                {
                    int lastIndex = pnlExportHistory.Controls.Count - 1;
                    Control oldest = pnlExportHistory.Controls[lastIndex];
                    pnlExportHistory.Controls.RemoveAt(lastIndex);
                    oldest.Dispose();
                }
            }
            finally
            {
                pnlExportHistory.ResumeLayout(true);
            }
        }

        private void ClearExportHistoryLinks()
        {
            exportHistory.Clear();
            pendingExportHistoryEntries.Clear();
            foreach (Control control in pnlExportHistory.Controls.Cast<Control>().ToArray())
            {
                pnlExportHistory.Controls.Remove(control);
                control.Dispose();
            }
        }

        private void UpdateHistoryLinkWidths()
        {
            if (pnlExportHistory == null || pnlExportHistory.IsDisposed) return;
            int width = Math.Max(80, pnlExportHistory.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
            foreach (Control control in pnlExportHistory.Controls)
                control.Width = width;
        }

        private void ExportHistoryLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is not LinkLabel link) return;
            if (link.Tag is not ExportHistoryRecord entry) return;
            link.LinkVisited = true;
            OpenExportLocation(entry);
        }

        private static string GetHistoryDisplayPath(string assetPath)
        {
            string relative = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(relative)) return string.Empty;

            relative = relative.Replace('\\', '/').TrimStart('/');
            int exportMarker = relative.IndexOf("/Export/", StringComparison.OrdinalIgnoreCase);
            if (exportMarker >= 0)
                relative = relative.Substring(exportMarker + "/Export/".Length);

            int contentMarker = relative.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
            if (contentMarker > 0)
            {
                int precedingSeparator = relative.LastIndexOf('/', contentMarker - 1);

                if (precedingSeparator >= 0)
                    relative = relative.Substring(precedingSeparator + 1);
            }

            return relative.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string GetExpectedExportPath(string outputDirectory, string assetPath)
        {
            string relative = NormalizeAssetPath(assetPath).Replace('/', Path.DirectorySeparatorChar);
            relative = relative.TrimStart(Path.DirectorySeparatorChar);
            try { return Path.GetFullPath(Path.Combine(outputDirectory ?? GetExportDirectory(), relative)); }
            catch { return Path.Combine(outputDirectory ?? GetExportDirectory(), relative); }
        }

        private void OpenExportLocation(ExportHistoryRecord entry)
        {
            if (entry == null) return;

            string output = entry.OutputDirectory;
            if (string.IsNullOrWhiteSpace(output) || !Directory.Exists(output)) output = GetExportDirectory();
            try
            {
                string expectedPath = GetExpectedExportPath(output, entry.AssetPath);
                string expectedDirectory = Path.GetDirectoryName(expectedPath);
                string folderToOpen = !string.IsNullOrWhiteSpace(expectedDirectory) && Directory.Exists(expectedDirectory)
                    ? expectedDirectory : null;

                if (string.IsNullOrWhiteSpace(folderToOpen))
                {
                    string exportedFile = File.Exists(expectedPath)
                        ? expectedPath
                        : FindExportedFileByStem(output, Path.GetFileNameWithoutExtension(entry.AssetPath));
                    if (!string.IsNullOrWhiteSpace(exportedFile) && File.Exists(exportedFile))
                        folderToOpen = Path.GetDirectoryName(exportedFile);
                }

                if (string.IsNullOrWhiteSpace(folderToOpen) || !Directory.Exists(folderToOpen))
                    folderToOpen = output;
                Directory.CreateDirectory(folderToOpen);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + folderToOpen + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not open export location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string FindExportedFileByStem(string outputDirectory, string fileStem)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory) || string.IsNullOrWhiteSpace(fileStem))
                return null;
            try
            {
                string[] matches = [.. Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), fileStem, StringComparison.OrdinalIgnoreCase))
                    .Take(2)];

                return matches.Length == 1 ? matches[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private sealed class ExportHistoryRecord
        {
            public string AssetPath { get; set; }
            public string OutputDirectory { get; set; }

        }

        private static int RunProcess(string exe, string[] args)
        {
            var start = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", args.Select(QuoteArgument)),
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exe)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = start };
            process.Start();
            Task stdoutDrain = Task.Factory.StartNew(
                () => DrainReader(process.StandardOutput),
                System.Threading.CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            Task stderrDrain = Task.Factory.StartNew(
                () => DrainReader(process.StandardError),
                System.Threading.CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            if (!process.WaitForExit((int)TimeSpan.FromMinutes(30).TotalMilliseconds))
            {
                try { process.Kill(); } catch { }
                process.WaitForExit();
                try { Task.WaitAll(stdoutDrain, stderrDrain); } catch { }
                throw new TimeoutException("retoc export did not finish within 30 minutes.");
            }

            process.WaitForExit();
            Task.WaitAll(stdoutDrain, stderrDrain);
            return process.ExitCode;
        }

        private static void DrainReader(TextReader reader)
        {
            var buffer = new char[4096];
            while (reader.Read(buffer, 0, buffer.Length) > 0) { }
        }

        private static string QuoteArgument(string argument)
        {
            if (argument == null) return "\"\"";
            if (argument.Length > 0 && !argument.Any(c => char.IsWhiteSpace(c) || c == '"')) return argument;
            var builder = new StringBuilder();
            builder.Append('"');
            int backslashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }
                if (backslashes > 0) { builder.Append('\\', backslashes); backslashes = 0; }
                builder.Append(c);
            }
            if (backslashes > 0) builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            btnBrowsePaks.Enabled = !value;
            cmbProfile.Enabled = !value;
            btnNewProfile.Enabled = !value;
            btnDeleteProfile.Enabled = !value;
            txtPaks.Enabled = !value;
            txtAesKey.Enabled = !value;
            cmbVersion.Enabled = !value;
            txtSearch.Enabled = !value;
            lstAssets.Enabled = !value;
            trvFolders.Enabled = !value;
            Cursor = value ? Cursors.WaitCursor : Cursors.Default;
        }

        private sealed class ContainerInventory
        {
            public string[] TocFiles { get; set; } = [];
            public List<ContainerScanEntry> Containers { get; set; } = [];
            public bool GlobalContainerExcluded { get; set; }
        }

        private sealed class ContainerScanEntry
        {
            public string Key { get; set; }
            public string UcasPath { get; set; }
            public string TocPath { get; set; }
            public bool UcasExists { get; set; }
            public bool HasToc { get; set; }
            public List<string> UcasFiles { get; set; } = [];
            public HashSet<string> Paths { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class AssetContainer
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public string ToolTip { get; set; }
            public string[] PackageAssets { get; set; } = [];
            public int AssetCount { get; set; }
            public Dictionary<string, List<string>> FolderChildren { get; set; } =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> FolderAssetCounts { get; set; } =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, AssetRange> FolderAssetRanges { get; set; } =
                new Dictionary<string, AssetRange>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class FolderTreeIndex
        {
            public string[] PackageAssets { get; set; } = [];
            public Dictionary<string, List<string>> FolderChildren { get; set; } =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> FolderAssetCounts { get; } =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, AssetRange> FolderAssetRanges { get; } =
                new Dictionary<string, AssetRange>(StringComparer.OrdinalIgnoreCase);
        }

        private struct AssetRange(int start, int count)
        {
            public int Start { get; private set; } = start;
            public int Count { get; private set; } = count;
        }

        private sealed class FolderNodeInfo
        {
            public string ContainerKey { get; set; }
            public string FolderPath { get; set; }
            public string DisplayName { get; set; }
        }

        private sealed class ListingResult
        {
            public HashSet<string> Paths { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<ChunkListingEntry> ChunkEntries { get; set; } = [];
        }

        private struct ChunkListingEntry
        {
            public string ChunkId { get; set; }
            public long SizeBytes { get; set; }
            public string Path { get; set; }
        }

        private struct PackageChunkSize
        {
            public long SizeBytes;
            public string PackagePath;
        }

        private async void MainForm_Load(object sender, EventArgs e) => await CheckForUpdatesAsync();
    }
}
