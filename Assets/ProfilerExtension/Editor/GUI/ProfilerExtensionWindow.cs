using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace ProfilerExtension
{
    public partial class ProfilerExtensionWindow : EditorWindow
    {
        [MenuItem("Tools/ProfilerExtension/Window")]
        static void OpenView()
        {
            ProfilerExtensionWindow win = GetWindow<ProfilerExtensionWindow>();
            win.titleContent = new GUIContent("Profiler_EX");
            win.position = new Rect(400, 200, 800, 660);
            if (!Directory.Exists(SaveFolder))
            {
                Directory.CreateDirectory(SaveFolder);
            }
        }

        static string s_TXTSavePath
        {
            get
            {
                string path = "C:/";
                return path;
            }
        }

        private static Stopwatch s_StopWatch = new Stopwatch();

        [MenuItem("Tools/ProfilerExtension/抓取内存快照 #F6")]
        public static void TakeSample()
        {
            s_StopWatch.Start();
            StandardOutputOption outputOption = new StandardOutputOption(Path.Combine(s_TXTSavePath, "内存信息.txt"));
            MemorySampleAgent.TakeMemorySample((root) =>
            {
                outputOption.Serialize(root);
                s_StopWatch.Stop();
                s_StopWatch.Reset();
                Debug.Log("抓取完成, cost : " + s_StopWatch.ElapsedMilliseconds / 1000f + "s");
            }, outputOption.Filter);
        }

        [MenuItem("Tools/ProfilerExtension/对比内存快照 #F1")]
        public static void CompareSample()
        {
            s_StopWatch.Start();
            StandardOutputOption outputOption = new StandardOutputOption(Path.Combine(s_TXTSavePath, "内存新增信息.txt"));
            outputOption.ComparePath = Path.Combine(s_TXTSavePath, "内存信息.txt");
            MemorySampleAgent.TakeMemorySample((root) =>
            {
                List<string> existInfos = MemorySampleAgent.LoadExistMemoryInfos(outputOption.ComparePath);
                if (existInfos == null) return;
                outputOption.CompareInfo(ref root, existInfos);
                outputOption.Serialize(root);
                s_StopWatch.Stop();
                s_StopWatch.Reset();
                Debug.Log("对比完成,cost : " + s_StopWatch.ElapsedMilliseconds / 1000f + "s");
                System.Diagnostics.Process.Start(outputOption.GetOutputPath());

            }, outputOption.Filter);
        }


        public static string SaveFolder
        {
            get
            {
                var folder = EditorPrefs.GetString("ProfilerSnapshotCacheFolder");
                if (string.IsNullOrEmpty(folder))
                {
                    EditorPrefs.SetString("ProfilerSnapshotCacheFolder", "C:/内存快照信息");
                }
                return folder;
            }
            set
            {
                EditorPrefs.SetString("ProfilerSnapshotCacheFolder", value);
            }
        }

        private List<MemorySnapshot> s_Snapshots = new List<MemorySnapshot>();
        private List<MemorySnapshot> s_SnapshotsToRemove = new List<MemorySnapshot>();

        private MemorySnapshot m_SnapshotToCompare1;
        private MemorySnapshot m_SnapshotToCompare2;

        private InfoTreeView m_MemorySampleTreeView;
        private TreeViewState m_TreeViewState;
        private SearchField m_SearchField;
        private string m_LastSelectPath;
        private MemorySnapshot m_CurDrawSnapshot;

        private Dynamic m_DynamicAttachProfilerUI;

        private void OnEnable()
        {
            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();
            m_SearchField = new SearchField();
            m_MemorySampleTreeView = new InfoTreeView(m_TreeViewState);
            m_SearchField.downOrUpArrowKeyPressed += m_MemorySampleTreeView.SetFocusAndEnsureSelectedItem;

            var typeAttachProfilerUI = typeof(EditorWindow).Assembly.GetType("UnityEditor.AttachProfilerUI");

            var attachProfilerUI = Activator.CreateInstance(typeAttachProfilerUI);
            m_DynamicAttachProfilerUI = new Dynamic(attachProfilerUI);
            OpenProfilerWindow();
        }

        private void OnGUI()
        {
            DrawSnapshotList();
            DrawHandleContent();
            DrawMemoryOverview();
            DrawMemoryDetail();
            RemoveSnapshot();
        }

        private static void OpenProfilerWindow()
        {
            if (ProfilerAdapter.GetProfilerWnd() == null)
            {
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow"));
                ProfilerExtensionWindow win = GetWindow<ProfilerExtensionWindow>();
                win.Show();
            }
        }

        void DrawSnapshotList()
        {
            int colCount = 6;
            int rowCount = Mathf.CeilToInt((float)s_Snapshots.Count / colCount);
            for (int i = 0; i < rowCount; i++)
            {
                GUILayout.BeginHorizontal();
                int startIndex = i * colCount;
                int endIndex = Mathf.Min((i + 1) * colCount, s_Snapshots.Count);
                for (int j = startIndex; j < endIndex; j++)
                {
                    GUILayout.BeginVertical();
                    var snapshot = s_Snapshots[j];
                    int itemWidth = 120;
                    snapshot.Key = GUILayout.TextField(snapshot.Key, GUILayout.MaxWidth(itemWidth));
                    GUILayout.BeginHorizontal();
                    var color = GUI.color;
                    if (m_CurDrawSnapshot == snapshot)
                    {
                        GUI.color = Color.cyan;
                    }
                    if (GUILayout.Button("显示", GUILayout.MaxWidth(itemWidth / 3), GUILayout.MinHeight(22)))
                    {
                        ShowSnapshot(snapshot);
                    }
                    GUI.color = color;
                    if (GUILayout.Button("保存", GUILayout.MaxWidth(itemWidth / 3), GUILayout.MinHeight(22)))
                    {
                        SaveSnapshot(snapshot);
                    }
                    if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUILayout.MaxWidth(itemWidth * 1.5f / 6)))
                    {
                        s_SnapshotsToRemove.Add(snapshot);
                    }


                    GUILayout.EndHorizontal();

                    if (m_SnapshotToCompare1 != null)
                    {
                        if (m_SnapshotToCompare2 == null)
                        {
                            if (snapshot == m_SnapshotToCompare1)
                            {
                                //GUI.enabled = false;
                                GUI.color = Color.gray;
                            }
                            else
                            {
                                GUI.color = Color.green;
                            }
                        }
                        else
                        {
                            if (m_SnapshotToCompare1 == snapshot || m_SnapshotToCompare2 == snapshot)
                            {
                                //GUI.enabled = false;
                                GUI.color = Color.cyan;
                            }
                        }
                    }
                    string btnText = "对比";
                    if (m_SnapshotToCompare1 == snapshot)
                    {
                        btnText = "对比(Before)";
                    }
                    else if (m_SnapshotToCompare2 == snapshot)
                    {
                        btnText = "对比(After)";
                    }
                    if (GUILayout.Button(btnText, GUILayout.MaxWidth(itemWidth)))
                    {
                        if (m_SnapshotToCompare1 == null)
                        {
                            m_SnapshotToCompare1 = snapshot;
                        }
                        else if (m_SnapshotToCompare1 != null && m_SnapshotToCompare2 != null)
                        {
                            m_SnapshotToCompare1 = snapshot;
                            m_SnapshotToCompare2 = null;
                        }
                        else if (m_SnapshotToCompare1 == snapshot)
                        {
                            m_SnapshotToCompare1 = null;
                        }
                        else
                        {
                            m_SnapshotToCompare2 = snapshot;
                            CompareSnapshot(m_SnapshotToCompare1, m_SnapshotToCompare2);
                        }

                    }
                    GUI.color = color;
                    GUI.enabled = true;
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }

        void DrawHandleContent()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool t = false;
            m_DynamicAttachProfilerUI.CallPublicInstanceMethod("OnGUILayout",
                new object[] {(EditorWindow)this});

            if (GUILayout.Button("截取快照", EditorStyles.toolbarButton))
            {
                OpenProfilerWindow();
                MemorySampleAgent.TakeMemorySnapshot((snapshot) =>
                {
                    s_Snapshots.Add(snapshot);
                    if (m_SnapshotToCompare1 == null)
                    {
                        ShowSnapshot(snapshot);
                    }
                });
            }
            if (GUILayout.Button("加载快照", EditorStyles.toolbarButton))
            {
                LoadSnapshot();
            }
            if (GUILayout.Button("选择保存目录", EditorStyles.toolbarButton))
            {
                SelectFolder();
            }
            GUILayout.EndHorizontal();

        }

        void ShowSnapshot(MemorySnapshot snapshot, bool compare = false)
        {
            if (!compare)
            {
                m_SnapshotToCompare1 = null;
                m_SnapshotToCompare2 = null;
            }
            m_CurDrawSnapshot = snapshot;
            m_MemorySampleTreeView.SetData(snapshot.MemorySample);
        }

        void RemoveSnapshot()
        {
            foreach (var snapshot in s_SnapshotsToRemove)
            {
                s_Snapshots.Remove(snapshot);
            }
        }

        void SaveSnapshot(MemorySnapshot snapshot)
        {
            var path = Path.Combine(SaveFolder, snapshot.Key.Replace(" ", "_").Replace(":", "-")) + ".snapshot";
            MemorySnapshot.Serialize(path, snapshot);
            Debug.Log("保存路径: " + path);
        }

        void LoadSnapshot()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("请选择文件", SaveFolder, new string[] { "*.*", "*.*" });
            if (!string.IsNullOrEmpty(path))
            {
                MemorySnapshot snapshot = MemorySnapshot.Deserialize(path);
                s_Snapshots.Add(snapshot);
            }
        }

        void CompareSnapshot(MemorySnapshot before, MemorySnapshot after)
        {
            m_SnapshotToCompare1 = before;
            m_SnapshotToCompare2 = after;
            var result = MemorySampleAgent.CompareSample(before, after);
            ShowSnapshot(result, true);
        }

        void DrawMemoryOverview()
        {
            if (m_CurDrawSnapshot == null) return;
            GUIStyle s = new GUIStyle();
            s.alignment = TextAnchor.MiddleCenter;
            s.fontSize = 12;
            GUILayout.Label(m_CurDrawSnapshot.Key, s);
            var text = m_CurDrawSnapshot.OverviewText;
            GUILayout.Label(text);
            //var rect = EditorGUILayout.GetControlRect(false, 200);
            //GUI.Label(rect, sb.ToString());
        }

        void DrawSearchField()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            m_MemorySampleTreeView.searchString = m_SearchField.OnToolbarGUI(m_MemorySampleTreeView.searchString);
            GUILayout.EndHorizontal();
        }

        void DrawMemoryDetail()
        {
            DrawSearchField();
            var rect = EditorGUILayout.GetControlRect(false, 300);
            if (m_MemorySampleTreeView == null) return;
            m_MemorySampleTreeView.OnGUI(rect);
            if (!string.IsNullOrEmpty(m_MemorySampleTreeView.searchString))
            {
                m_LastSelectPath = m_MemorySampleTreeView.searchString;
            }
            if (string.IsNullOrEmpty(m_MemorySampleTreeView.searchString) && !string.IsNullOrEmpty(m_LastSelectPath))
            {
                //var selection = m_MemorySampleTreeView.GetSelection();
                //if (selection != null && selection.Count > 0)
                //{
                //    m_MemorySampleTreeView.FrameItem(selection[0]);
                //}
                m_LastSelectPath = null;
            }
        }

        private void SelectFolder()
        {
            string path = EditorUtility.OpenFolderPanel("请选择保存目录", SaveFolder, " ");
            if (!string.IsNullOrEmpty(path))
            {
                SaveFolder = path;
            }
        }


        void Update()
        {
            Repaint();
        }

    }

    class MemoryElementTreeViewItem : TreeViewItem
    {
        public MemoryElement memoryElement;
        public int childCount;
    }

    class InfoTreeView : TreeView
    {
        private MemoryElementTreeViewItem m_RootItem;
        public InfoTreeView(TreeViewState treeViewState)
            : base(treeViewState)
        {
            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            Reload();
        }

        private MemoryElement m_Root;

        public void SetData(MemoryElement root)
        {
            this.m_Root = root;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            m_RootItem = new MemoryElementTreeViewItem { id = 0, depth = -1, displayName = "Root", memoryElement = m_Root };
            TraceTree(m_RootItem);
            if (!m_RootItem.hasChildren)
            {
                m_RootItem.AddChild(new TreeViewItem()
                {
                    id = GetGUID(),
                    depth = m_RootItem.depth + 1,
                    displayName = ""
                });
            }

            return m_RootItem;
        }

        private int id = 0;
        private int GetGUID()
        {
            return id++;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);
            Rect rect = args.rowRect;
            if (!(args.item is MemoryElementTreeViewItem)) return;
            var item = (MemoryElementTreeViewItem)args.item;
            var memoryElement = item.memoryElement;
            rect.x += 32f * (item.depth + 1) + rect.width * 0.5f;
            GUI.Label(rect, string.Format("{0}MB", memoryElement.GetMemoryMB().ToString("f4")));
            
        }

        public void TraceTree(MemoryElementTreeViewItem item)
        {
            if (item == null || item.memoryElement == null) return;
            var childMemoryElements = item.memoryElement.children;
            for (int i = 0; i < childMemoryElements.Count; i++)
            {
                var childElement = childMemoryElements[i];
                string childCountStr = childElement.children.Count > 1 ? string.Format("({0})", childElement.children.Count) : "";
                var newItem = new MemoryElementTreeViewItem()
                {
                    id = GetGUID(),
                    depth = item.depth + 1,
                    memoryElement = childElement,
                    childCount = childElement.children.Count,
                    displayName = childElement.name + childCountStr
                };
                item.AddChild(newItem);
                TraceTree(newItem);
            }
        }

        private static string AbsDirToSubDir(string absDir)
        {
            return absDir.Replace('\\', '/').Replace(Application.dataPath, "Assets");
        }
    }


}

