using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Text;

namespace ProfilerExtension
{
    public abstract class SampleOutputOption
    {
        protected bool m_IsCompareMode = false;

        protected static string m_OutputPath;

        protected string m_ComparePath;

        public string ComparePath
        {
            get { return m_ComparePath; }
            set { m_ComparePath = value; }
        }

        protected Dictionary<int, HashSet<string>> m_filter;

        public Dictionary<int, HashSet<string>> Filter
        {
            get { return m_filter; }
        }

        protected HashSet<string> m_CompareFilter;

        public HashSet<string> CompareFilter
        {
            get { return m_CompareFilter; }
        }

        public SampleOutputOption(string _outputPath)
        {
            m_OutputPath = _outputPath;
        }

        public SampleOutputOption()
        {

        }

        public abstract bool NeedSerialized(MemoryElement str);

        public abstract string Fromat(MemoryElement element, int depth);

        public abstract string GetOutputPath();

        public virtual void CompareInfo(ref MemoryElement root, List<string> existInfo)
        {
        }

        public virtual string GetHead()
        {
            return null;
        }

        public virtual void Serialize(MemoryElement root)
        {
            SaveInfo(GetOutputPath(), SerailizeToString(root));
        }

        public virtual string SerailizeToString(MemoryElement root)
        {
            StringBuilder sb = new StringBuilder();
            List<string> infos = new List<string>();
            string headInfo = GetHead();
            if (!string.IsNullOrEmpty(headInfo))
            {
                infos.Add(headInfo);
                sb.AppendLine(headInfo);
            }
            WriteMemoryDetail(sb, root);
            return sb.ToString();
        }

        private void WriteMemoryDetail(StringBuilder sb, MemoryElement element)
        {
            if (null == element)
                return;
            if (NeedSerialized(element))
            {
                string infoStr = Fromat(element, element.depth);
                sb.AppendLine(infoStr);
            }
            for (int index = 0; index < element.children.Count; ++index)
            {
                MemoryElement child = element.children[index];
                if (null != child)
                    WriteMemoryDetail(sb, child);
            }
        }

        private void SaveInfo(string path, string content)
        {
            File.WriteAllText(path, content);
        }

    }

    public class StandardOutputOption : SampleOutputOption
    {
        public static new List<string> S_TabSymbols = new List<string>()
        {
            "",
            "\t",
            "\t\t",
            "\t\t\t",
            "\t\t\t\t"
        };

        static Regex s_CountRegex = new Regex(@"(?<=\<)\d+");
        static Regex s_NameRegex = new Regex(@"^[^|<]+");
        static Regex s_SizeRegex = new Regex(@"(?<=\|)[\d.]+");

        public StandardOutputOption(string _outputPath) : base(_outputPath)
        {
            m_filter = new Dictionary<int, HashSet<string>>()
            {
                {1, new HashSet<string>() {"Assets", "Other", "Not Saved"}},
                {
                    2, new HashSet<string>()
                    {
                        "Texture2D",
                        "SerializedFile",
                        "ShaderLab",
                        "Mesh",
                        "AssetBundle",
                        "AnimationClip",
                        "AnimatorController",
                        "AnimatorOverrideController",
                        "Prefab",
                        "GameObject"
                    }
                },
            };
            m_CompareFilter = new HashSet<string>()
            {
                "Assets-Texture2D",
                "Assets-Mesh",
                "Assets-AnimationClip",
                "Assets-AnimatorController",
                "Assets-AnimatorOverrideController",
                "Other-SerializedFile",
                "Other-ShaderLab",
                "Not Saved-AssetBundle",
                "Not Saved-Mesh",
                "Assets-Prefab",
            };
        }

        public StandardOutputOption()
        {

        }

        public override bool NeedSerialized(MemoryElement str)
        {
            return true;
        }

        public override string Fromat(MemoryElement element, int depth)
        {
            depth = depth >= S_TabSymbols.Count ? S_TabSymbols.Count - 1 : depth;
            string tabsymbol = S_TabSymbols[depth];
            var childCount = element.children.Count;
            string childCountStr = childCount > 1 ? string.Format("<{0}>", childCount) : "";
            return string.Format("{0}{1}{2}|{3} MB", tabsymbol, element.name, childCountStr,
                element.GetMemoryMB().ToString("f5"));
        }

        public override string GetOutputPath()
        {
            return m_OutputPath;
        }

        public MemoryElement DeserializeMemoryInfo(List<string> info)
        {
            Dictionary<int, MemoryElement> tempDict = new Dictionary<int, MemoryElement>();
            foreach (var rawInfo in info)
            {
                if (string.IsNullOrEmpty(rawInfo)) continue;
                string tempStr1 = rawInfo.Replace("\t", "");
                int depth = rawInfo.Length - tempStr1.Length - 1;
                string itemName = s_NameRegex.Match(tempStr1).Value;
                string sizeStr = s_SizeRegex.Match(tempStr1).Value;
                float size = float.Parse(sizeStr);
                MemoryElement element = new MemoryElement()
                {
                    depth = depth,
                    name = itemName,
                    totalMemory = size * 1024f * 1024f,
                };
                tempDict[depth] = element;
                if (depth > -1)
                {
                    var parent = tempDict[depth - 1];
                    parent.children.Add(element);
                    element.parent = parent;
                }

            }
            return tempDict[-1];
        }

        public class LeafElementgroup1
        {
            public Dictionary<string, List<MemoryElement>> elements = new Dictionary<string, List<MemoryElement>>();
            public float memory = 0;
            public MemoryElement parent;
            public MemoryElement temp;

            public void AddElement(float mem, MemoryElement element)
            {
                memory += mem;
                var key = mem.ToString("f5");
                if (!elements.ContainsKey(key))
                {
                    elements.Add(key, new List<MemoryElement>());
                }
                elements[key].Add(element);
            }
        }

        public Dictionary<string, LeafElementgroup1> ParseElement(MemoryElement root)
        {
            Dictionary<string, LeafElementgroup1> dict = new Dictionary<string, LeafElementgroup1>();
            Stack<MemoryElement> stack = new Stack<MemoryElement>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var curElement = stack.Pop();
                var children = curElement.children;
                if (children.Count == 0)
                {
                    string key = curElement.GetPathToRoot();
                    if (!dict.ContainsKey(key))
                    {
                        dict[key] = new LeafElementgroup1() {parent = curElement.parent, temp = curElement};
                    }
                    dict[key].AddElement(curElement.GetMemoryMB(), curElement);
                }
                else
                {
                    foreach (var child in children)
                    {
                        stack.Push(child);
                    }
                }
            }
            return dict;
        }

        public override void CompareInfo(ref MemoryElement root1, List<string> existInfos)
        {
            MemoryElement root2 = DeserializeMemoryInfo(existInfos);
            CompareInfo(ref root1, root2);
        }

        public void CompareInfo(ref MemoryElement root1, MemoryElement root2)
        {
            root2.name = "";
            var dict1 = ParseElement(root1);
            var dict2 = ParseElement(root2);
            List<MemoryElement> emptyElementsToRemove = new List<MemoryElement>();
            foreach (var kv in dict1)
            {
                var path = kv.Key;
                var group1 = kv.Value;
                if (!dict2.ContainsKey(path)) continue;
                var group2 = dict2[path];
                foreach (var key in group1.elements.Keys.ToArray())
                {
                    if (group2.elements.ContainsKey(key))
                    {
                        var list1 = group1.elements[key];
                        var list2 = group2.elements[key];
                        int removeCount = Mathf.Min(list1.Count, list2.Count);
                        for (int t = removeCount - 1; t >= 0; t--)
                        {
                            var element = list1[t];
                            group1.memory -= element.totalMemory;
                            group1.parent.children.Remove(element);
                            element.parent = null;
                            list1.RemoveAt(t);
                            list2.RemoveAt(t);
                        }
                        if (list1.Count == 0)
                        {
                            group1.elements.Remove(key);
                        }
                        if (list2.Count == 0)
                        {
                            group2.elements.Remove(key);
                        }
                    }
                    if (group1.memory > 10 && group1.parent.children.Count == 1)
                    {
                        group1.parent.children[0].totalMemory = group1.memory;
                    }
                    if (group1.parent.children.Count == 0)
                    {
                        emptyElementsToRemove.Add(group1.parent);
                    }
                }
                if (group1.elements.Count == group2.elements.Count && group1.elements.Count == 1)
                {
                    var memory = group1.temp.totalMemory - group2.temp.totalMemory;
                    if (memory < 0)
                    {
                        if (group1.temp.parent != null)
                        {
                            group1.temp.parent.children.Remove(group1.temp);
                            group1.temp.parent = null;
                        }
                    }
                    else
                    {
                        group1.temp.totalMemory = memory;
                    }
                }
            }
            RemoveEmptyParentRoot(ref root1, emptyElementsToRemove);
            ReCalculateMemory(ref root1);
        }

        public void RemoveEmptyParentRoot(ref MemoryElement root, List<MemoryElement> emptyElementsToRemove)
        {
            Queue<MemoryElement> queue = new Queue<MemoryElement>();
            HashSet<MemoryElement> set = new HashSet<MemoryElement>();
            foreach (var e in emptyElementsToRemove)
            {
                queue.Enqueue(e);
            }
            while (queue.Count > 0)
            {
                var element = queue.Dequeue();
                if (element.children.Count == 0)
                {
                    var parent = element.parent;
                    if (parent != null)
                    {
                        parent.children.Remove(element);
                        element.parent = null;
                        if (!set.Contains(element))
                        {
                            queue.Enqueue(parent);
                        }
                    }
                }
            }
        }

        public void ReCalculateMemory(ref MemoryElement root)
        {
            Stack<MemoryElement> stack1 = new Stack<MemoryElement>();
            Stack<MemoryElement> stack2 = new Stack<MemoryElement>();
            stack1.Push(root);
            stack2.Push(root);
            while (stack1.Count > 0)
            {
                var curElement = stack1.Pop();
                var children = curElement.children;
                if (children.Count != 0)
                {
                    foreach (var child in children)
                    {
                        stack1.Push(child);
                    }
                    stack2.Push(curElement);
                }
            }
            while (stack2.Count > 0)
            {
                var curElement = stack2.Pop();
                float size = 0;
                foreach (var child in curElement.children)
                {
                    size += child.totalMemory;
                }
                curElement.totalMemory = size;
            }

        }

    }

}