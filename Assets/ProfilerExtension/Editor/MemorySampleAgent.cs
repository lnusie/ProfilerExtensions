using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using Debug = UnityEngine.Debug;

namespace ProfilerExtension
{
    public class MemoryItemInfo
    {
        public string m_ItemName;
        public float m_Value;
        public string m_UnitName;

        public float TotalMB()
        {
            switch (m_UnitName)
            {
                case "KB": return m_Value / 1024f;
                case "GB": return m_Value * 1024f;
                default: return m_Value;
            }
        }
    }

    public class MemorySnapshot
    {

        private const string BASE_HEAD = "----BASE----";

        private const string OVERVIEW_HEAD = "----OVERVIEW----";

        private const string SAMPLE_HEAD = "----SAMPLE-----";

        private string m_Key;

        public string Key
        {
            get
            {
                if (string.IsNullOrEmpty(m_Key))
                {
                    DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
                    DateTime dt = startTime.AddSeconds(CreateTime);
                    m_Key = dt.ToString("MM-dd HH:mm:ss");
                }
                return m_Key;
            }
            set { m_Key = value; }

        }

        private long m_CreateTime;

        public long CreateTime
        {
            get { return m_CreateTime; }
        }

        private string m_OverviewText;

        public string OverviewText
        {
            get
            {
                if (null == m_OverviewText && null != m_OverviewInfo)
                {
                    m_OverviewText = MemorySampleAgent.ConverMemoryInfoToOverviewText(m_OverviewInfo);
                }
                return m_OverviewText;
            }

        }

        private List<MemoryItemInfo> m_OverviewInfo;

        public List<MemoryItemInfo> OverviewInfo
        {
            get
            {
                if (null == m_OverviewInfo && null != m_OverviewText)
                {
                    m_OverviewInfo = MemorySampleAgent.ConvertMemoryOverviewText(m_OverviewText);
                }
                return m_OverviewInfo;
            }
            set { m_OverviewInfo = value; }
        }

        private MemoryElement m_MemorySample;

        public MemoryElement MemorySample
        {
            get { return m_MemorySample; }
            set { m_MemorySample = value; }
        }

        public MemorySnapshot()
        {

        }

        public MemorySnapshot(long createTime, string overviewText, MemoryElement memorySample)
        {
            m_CreateTime = createTime;
            m_OverviewText = overviewText;
            m_MemorySample = memorySample;
        }

        public static string SerailizeToString(MemorySnapshot snapshot)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n" + BASE_HEAD);
            sb.Append(string.Format("m_CreateTime:{0},m_Key:{1}", snapshot.CreateTime, snapshot.Key));
            sb.AppendLine("\n" + OVERVIEW_HEAD);
            sb.Append(snapshot.OverviewText);
            sb.AppendLine("\n" + SAMPLE_HEAD);
            sb.Append(MemorySampleAgent.SerializeSample(snapshot.MemorySample));
            return sb.ToString();
        }

        public static void Serialize(string savePath, MemorySnapshot snapshot)
        {
            var content = SerailizeToString(snapshot);
            File.WriteAllText(savePath, content);
        }

        public static MemorySnapshot Deserialize(string filePath)
        {
            MemorySnapshot snapshot = new MemorySnapshot();
            var textLines = File.ReadAllLines(filePath);
            List<string> lines1 = new List<string>();
            List<string> lines2 = new List<string>();
            List<string> lines = null;
            for (int i = 0; i < textLines.Length; i++)
            {
                var line = textLines[i];
                if (line.Trim() == BASE_HEAD)
                {
                    var nextLine = textLines[i + 1];
                    string createTimeStr = new Regex(@"(?<=m_CreateTime:)\d+").Match(nextLine).Value;
                    string key = new Regex(@"(?<=m_Key:).+").Match(nextLine).Value;
                    snapshot.m_CreateTime = long.Parse(createTimeStr);
                    snapshot.Key = key;
                }

                if (line.Trim() == OVERVIEW_HEAD)
                {
                    lines = lines1;
                }
                else if (line.Trim() == SAMPLE_HEAD)
                {
                    lines = lines2;
                }
                else if (lines != null)
                {
                    lines.Add(line);
                }
            }
            string overviewText = string.Join("\n", lines1.ToArray());
            snapshot.m_OverviewText = overviewText;
            snapshot.m_MemorySample = MemorySampleAgent.DeserializeSample(lines2);
            return snapshot;
        }
    }

    public static class MemorySampleAgent
    {
        private static Action<MemoryElement> s_OnTakeSampleCallback;

        public static void TakeMemorySnapshot(Action<MemorySnapshot> callback)
        {
            TakeMemorySample((root) =>
            {
                MemorySnapshot info = new MemorySnapshot(GetNowTimeStamp(), TakeMemoryOverviewText(), root);
                callback.Invoke(info);
            });
        }


        public static MemorySnapshot CompareSample(MemorySnapshot snapshot1, MemorySnapshot snapshot2)
        {
            MemorySnapshot snapshot = new MemorySnapshot();
            snapshot.OverviewInfo = CompareMemoryOverviewInfo(snapshot2.OverviewInfo, snapshot1.OverviewInfo);
            var memoryElement = snapshot2.MemorySample.Clone() as MemoryElement;
            snapshot.MemorySample = memoryElement;
            snapshot.Key = string.Format("{0} -> {1}", snapshot1.Key, snapshot2.Key);
            StandardOutputOption outputOption = new StandardOutputOption();
            outputOption.CompareInfo(ref memoryElement, snapshot1.MemorySample);
            return snapshot;
        }

        public static void TakeMemorySample(Action<MemoryElement> callback,
            Dictionary<int, HashSet<string>> filter = null)
        {
            OpenProfilerWindow();
            s_OnTakeSampleCallback = callback;
            ProfilerAdapter.GrabMemorySnapshoot(OnGetMemoryInfo, filter);
        }

        public static void OnGetMemoryInfo(MemoryElement root)
        {
            if (s_OnTakeSampleCallback != null)
            {
                s_OnTakeSampleCallback.Invoke(root);
                s_OnTakeSampleCallback = null;
            }
        }

        public static List<string> LoadExistMemoryInfos(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError("请先截取快照再执行对比");
                return null;
            }
            List<string> existInfos = new List<string>();
            using (StreamReader reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line == null) continue;
                    //line = line.Trim().Replace(" ", ""); 
                    existInfos.Add(line);
                }
            }
            return existInfos;
        }

        public static long GetNowTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long now = Convert.ToInt64(ts.TotalSeconds);
            return now;
        }

        public static string SerializeSample(MemoryElement root)
        {
            StandardOutputOption option = new StandardOutputOption();
            return option.SerailizeToString(root);
        }

        public static MemoryElement DeserializeSample(List<string> lines)
        {
            StandardOutputOption option = new StandardOutputOption();
            return option.DeserializeMemoryInfo(lines);
        }

        #region "总览信息"

        public static string TakeMemoryOverviewText()
        {
            OpenProfilerWindow();
            var memoryWindowObj = ProfilerAdapter.GetProfilerWnd();
            int currentFrame = (int) memoryWindowObj.PrivateInstanceField("m_CurrentFrame");
            var profilerDriverType =
                new Dynamic(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditorInternal.ProfilerDriver"));
            string text =
                profilerDriverType.CallPublicStaticMethod("GetOverviewText",
                    new object[] {ProfilerArea.Memory, currentFrame}) as string;
            return text;
        }

        public static List<MemoryItemInfo> ConvertMemoryOverviewText(string text)
        {
            text = text.Replace("\n", "$");
            string pattern = @"[\w\s]+:\s[\s\d/.]+\s*[KMGB]*";
            Regex regex = new Regex(pattern);
            MatchCollection results = regex.Matches(text);
            List<MemoryItemInfo> memoryInfos = new List<MemoryItemInfo>();
            HashSet<string> hashSet = new HashSet<string>();
            var itemNameRegex = new Regex(@"[\w\s]+");
            var valueRegex = new Regex(@"[\d/.]+");
            var unitRegex = new Regex(@"[KMGB]+");

            for (int i = 0; i < results.Count; i++)
            {
                var m = results[i];
                var str = m.Value.Trim();
                var strs = str.Split(':');
                string itemName = strs[0];
                string unitName = unitRegex.Match(strs[1]).Value;
                string valueStr = valueRegex.Match(strs[1].Replace(" ", "")).Value;

                if (valueStr.Contains("/"))
                {
                    strs = valueStr.Split('/');
                    memoryInfos.Add(new MemoryItemInfo()
                    {
                        m_ItemName = itemName + " Count",
                        m_Value = float.Parse(strs[0]),
                        m_UnitName = ""
                    });
                    memoryInfos.Add(new MemoryItemInfo()
                    {
                        m_ItemName = itemName,
                        m_Value = float.Parse(strs[1]),
                        m_UnitName = unitName
                    });
                }
                else
                {
                    if (hashSet.Contains(itemName))
                    {
                        itemName = "Reserved " + itemName;
                    }
                    hashSet.Add(itemName);
                    memoryInfos.Add(new MemoryItemInfo()
                    {
                        m_ItemName = itemName,
                        m_Value = float.Parse(valueStr),
                        m_UnitName = unitName
                    });
                }

            }
            return memoryInfos;
        }

        public static List<MemoryItemInfo> CompareMemoryOverviewInfo(List<MemoryItemInfo> list1,
            List<MemoryItemInfo> list2)
        {
            List<MemoryItemInfo> result = new List<MemoryItemInfo>();
            Dictionary<string, MemoryItemInfo> dict = new Dictionary<string, MemoryItemInfo>();
            foreach (var info in list1)
            {
                if (!dict.ContainsKey(info.m_ItemName))
                {
                    var newInfo = (new MemoryItemInfo()
                    {
                        m_ItemName = info.m_ItemName,
                        m_UnitName = info.m_UnitName,
                        m_Value = info.m_Value
                    });
                    dict.Add(info.m_ItemName, newInfo);
                    result.Add(newInfo);
                }
            }
            foreach (var info2 in list2)
            {
                if (dict.ContainsKey(info2.m_ItemName))
                {
                    var info1 = dict[info2.m_ItemName];
                    if (info1.m_UnitName != info2.m_UnitName)
                    {
                        float diff = info1.TotalMB() - info2.TotalMB();
                        if (Math.Abs(diff) > 0.001f)
                        {
                            info1.m_Value = diff;
                            info1.m_UnitName = "MB";
                        }
                    }
                    else
                    {
                        info1.m_Value = info1.m_Value - info2.m_Value;
                    }
                }
            }
            return result;
        }

        public static string ConverMemoryInfoToOverviewText(List<MemoryItemInfo> list)
        {
            string template =
                @"Used Total: [Used Total]   Unity: [Unity]   Mono: [Mono]   GfxDriver: [GfxDriver]   FMOD: [FMOD]  Video: [Video]  Profiler: [Profiler] 
Reserved Total: [Reserved Total]   Unity: [Reserved Unity]   Mono: [Reserved Mono]   GfxDriver: [Reserved GfxDriver]   FMOD:[Reserved FMOD]   Video: [Reserved Video]   Profiler: [Reserved Profiler]   
Total System Memory Usage: [Total System Memory Usage]

Textures: [Textures Count] / [Textures] 
Meshes: [Meshes Count] / [Textures]
Materials: [Materials Count] / [Materials]
AnimationClips: [AnimationClips Count] / [AnimationClips]
AudioClips: [AudioClips Count] / [AudioClips]
Assets: [Assets] 
GameObjects in Scene: [GameObjects in Scene] 
Total Objects in Scene: [Total Objects in Scene] 
Total Object Count: [Total Object Count]
GC Allocations per Frame: [GC Allocations per Frame Count] / 2.0 KB";

            foreach (var info in list)
            {
                template = template.Replace("[" + info.m_ItemName + "]",
                    string.Format("{1} {2} ", info.m_ItemName, info.m_Value, info.m_UnitName));
            }
            return template;
        }

        #endregion

        private static void OpenProfilerWindow()
        {
            if (ProfilerAdapter.GetProfilerWnd() == null)
            {
                var win = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow"));
            }
        }
    }
}