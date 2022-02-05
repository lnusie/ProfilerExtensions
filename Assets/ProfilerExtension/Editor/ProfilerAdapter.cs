using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;


namespace ProfilerExtension
{
    public class ProfilerAdapter
    {
        public static bool installHooked = false;

        private static Action<MemoryElement> OnGetMemoryInfoCallback;

        public static Dictionary<int, HashSet<string>> GrapFilter = null;

        private static List<Dynamic> _wnds = (List<Dynamic>) null;

        public static Dynamic GetProfilerWnd(ProfilerArea area)
        {
            IList list = new DynamicType(typeof(EditorWindow)).GetType("UnityEditor.ProfilerWindow")
                .PrivateStaticField<IList>("m_ProfilerWindows");
            _wnds = new List<Dynamic>();
            for (int index = 0; index < list.Count; ++index)
                _wnds.Add(new Dynamic(list[index]));
            for (int index = 0; index < ProfilerAdapter._wnds.Count; ++index)
            {
                Dynamic wnd = ProfilerAdapter._wnds[index];
                if ((ProfilerArea) wnd.PrivateInstanceField("m_CurrentArea") == area)
                    return wnd;
            }
            return (Dynamic) null;
        }

        public static Dynamic GetProfilerWnd()
        {
            IList list = new DynamicType(typeof(EditorWindow)).GetType("UnityEditor.ProfilerWindow")
                .PrivateStaticField<IList>("m_ProfilerWindows");
            if (list != null && list.Count > 0)
            {
                return new Dynamic(list[0]);
            }
            return null;
        }

        public static void ClearMemoryDetailed()
        {
            new Dynamic(ProfilerAdapter.GetProfilerWnd(ProfilerArea.Memory).PrivateInstanceField("m_MemoryListView"))
                .CallPublicInstanceMethod("SetRoot", new object[1]);
        }

        /// <summary>
        /// 获取已抓取的快照
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static MemoryElement GetMemoryDetailRoot(Dictionary<int, HashSet<string>> filter)
        {
            Dynamic wnd = ProfilerAdapter.GetProfilerWnd(ProfilerArea.Memory);
            if (null == wnd)
                return (MemoryElement) null;
            object obj = new Dynamic(wnd.PrivateInstanceField("m_MemoryListView")).PrivateInstanceField("m_Root");
            if (null == obj)
                return (MemoryElement) null;
            return MemoryElement.Create(new Dynamic(obj), 0, filter);
        }

        public static void InstallMemoryProfilerHook()
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            var ProfilerWindowType = assembly.GetType("UnityEditor.ProfilerWindow");
            MethodInfo SetMemoryProfilerInfo = ProfilerWindowType.GetMethod("SetMemoryProfilerInfo",
                BindingFlags.Static | BindingFlags.NonPublic);
            Type replace = typeof(ProfilerAdapter);
            MethodInfo setMemoryProfilerInfoRelace = replace.GetMethod("SetMemoryProfilerInfoRelace",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo setMemoryProfilerInfoProxy = replace.GetMethod("SetMemoryProfilerInfoProxy",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodHook hooker = new MethodHook(SetMemoryProfilerInfo, setMemoryProfilerInfoRelace,
                setMemoryProfilerInfoProxy);
            hooker.Install();
           
        }

        public static void GrabMemorySnapshoot(Action<MemoryElement> getMemoryInfoCallback,
            Dictionary<int, HashSet<string>> filter, bool getReference = false)
        {
            if (getMemoryInfoCallback == null) return;
            OnGetMemoryInfoCallback = getMemoryInfoCallback;
            if (!installHooked)
            {
                InstallMemoryProfilerHook();
                installHooked = true;
            }
            GrapFilter = filter;
            Assembly assembly = typeof(EditorWindow).Assembly;
            var type = assembly.GetType("UnityEditorInternal.ProfilerDriver");
            var method = type.GetMethod("RequestObjectMemoryInfo");
            method.Invoke(null, new object[] {getReference});
        }

        private static void SetMemoryProfilerInfoRelace(ObjectMemoryInfo[] memoryInfo, int[] referencedIndices)
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            var GetTreeRootMethod = assembly.GetType("UnityEditor.MemoryElementDataManager").GetMethod("GetTreeRoot");
            object obj = GetTreeRootMethod.Invoke(null, new object[] {memoryInfo, referencedIndices});
            ExpandMemoryElementChildren(obj, 0);
            MemoryElement root = MemoryElement.Create(new Dynamic(obj), 0, GrapFilter);
            SetMemoryProfilerInfoProxy(memoryInfo, referencedIndices);
            if (root == null)
                return;
            if (OnGetMemoryInfoCallback != null)
            {
                OnGetMemoryInfoCallback.Invoke(root);
            }
            OnGetMemoryInfoCallback = null;
        }

        private static void SetMemoryProfilerInfoProxy(ObjectMemoryInfo[] memoryInfo, int[] referencedIndices)
        {
            Debug.LogError("SetMemoryProfilerInfoProxy");
            //Do nothing...
        }

        public static int MaxExpandChildDepth = 5;

        private static void ExpandMemoryElementChildren(object memoryElement, int depth)
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            var childrenField = typeof(EditorWindow).Assembly.GetType("UnityEditor.MemoryElement").GetField("children");
            var ExpandChildrenMethod = assembly.GetType("UnityEditor.MemoryElement").GetMethod("ExpandChildren");
            var childList = childrenField.GetValue(memoryElement) as IEnumerable;
            if (childList == null) return;
            foreach (var child in childList)
            {
                ExpandChildrenMethod.Invoke(child, null);
                if (depth < MaxExpandChildDepth)
                {
                    ExpandMemoryElementChildren(child, depth - 1);
                }
            }
        }


    }

}