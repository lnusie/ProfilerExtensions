using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using ProfilerExtension;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProfilerExtension
{
    [Serializable]
    public class MemoryElement : ICloneable
    {
        public MemoryElement parent;
        public List<MemoryElement> children = new List<MemoryElement>();
        public string name;
        public float totalMemory;
        public int depth;

        public MemoryElement()
        {
        }

        public static MemoryElement Create(Dynamic memoryEle, int dep, Dictionary<int, HashSet<string>> filter)
        {
            if (null == memoryEle)
                return (MemoryElement) null;
            string str = memoryEle.PublicInstanceField<string>("name");
            var memory = (long) memoryEle.PublicInstanceField("totalMemory");
            HashSet<string> stringSet = (HashSet<string>) null;
            if (filter != null && filter.TryGetValue(dep, out stringSet) && !stringSet.Contains(str))
                return (MemoryElement) null;
            MemoryElement memoryElement1 = new MemoryElement()
            {
                depth = dep,
                name = str,
                totalMemory = memory
            };
            Dynamic.ShallowCopyFrom((object) memoryElement1, memoryEle.InnerObject,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
            IList list = memoryEle.PublicInstanceField<IList>("children");
            if (null != list)
            {
                foreach (object obj in (IEnumerable) list)
                {
                    MemoryElement memoryElement2 = MemoryElement.Create(new Dynamic(obj), dep + 1, filter);
                    if (null != memoryElement2)
                    {
                        memoryElement1.children.Add(memoryElement2);
                        memoryElement2.parent = memoryElement1;
                    }
                }
                var childList = memoryElement1.children;
                memoryElement1.children = childList.OrderByDescending(m => m.totalMemory).ToList();
            }
            return memoryElement1;
        }

        public string GetPathToRoot()
        {
            string str = "";
            var e = this;
            while (e != null)
            {
                str = e.name + "/" + str;
                e = e.parent;
            }
            return str;
        }

        public float GetMemoryMB()
        {
            return totalMemory / (1024f * 1024f);
        }

        public override string ToString()
        {
            string str1 = string.IsNullOrEmpty(this.name) ? "-" : this.name;
            string str2 = "KB";
            float num = (float) this.totalMemory / 1024f;
            if ((double) num > 0)
            {
                num /= 1024f;
                str2 = "MB";
            }
            string numStr = num.ToString();
            //Debug.Log("numStr "+numStr);
            string[] strs = numStr.Split('.');
            if (strs.Length > 1)
            {
                string strBehindDot = strs[1];
                if (strBehindDot.Length > 2)
                {
                    numStr = strs[0] + "." + strBehindDot.Substring(0, 2);
                }
                else if (strBehindDot.Length > 1)
                {
                    numStr = strs[0] + "." + strBehindDot.Substring(0, 1);
                }
                else
                {
                    numStr = strs[0] + "." + "0";
                }
            }
            return string.Format("{0}{1},{2}{3}", this.depth, str1, numStr, (object) str2);
        }

        public object Clone()
        {
            MemoryElement newElement = new MemoryElement()
            {
                totalMemory = totalMemory,
                depth = depth,
                children = new List<MemoryElement>(children.Count),
                name = name
            };
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i].Clone() as MemoryElement;
                newElement.children.Add(child);
                child.parent = newElement;
            }
            return newElement;
        }

        public void Dump()
        {
            string str = string.Format("[{0}]{1}", depth, name);
            Debug.LogError(str);
            for (int i = 0; i < children.Count; i++)
            {
                children[i].Dump();
            }
        }

    }

}





