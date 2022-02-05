using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ProfilerExtension
{
    public class Dynamic
    {
        private const BindingFlags PublicInstanceFieldFlag =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField;

        private const BindingFlags PrivateInstanceFieldFlag =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField;

        private const BindingFlags PrivateStaticFieldFlag =
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.GetField;

        private const BindingFlags PublicInstanceMethodFlag = BindingFlags.Instance | BindingFlags.Public;
        public readonly Type InnerType;
        private object _obj;

        public object InnerObject
        {
            get { return this._obj; }
        }

        public Dynamic(Type innerType)
        {
            this.InnerType = innerType;
        }

        public Dynamic(object obj)
        {
            if (null == obj)
                return;
            this.InnerType = obj.GetType();
            this._obj = obj;
        }

        public static void ShallowCopyFrom(object dst, object src, BindingFlags flags)
        {
            if (dst == null || null == src)
                return;
            Type type1 = dst.GetType();
            Type type2 = src.GetType();
            FieldInfo[] fields = type1.GetFields(flags);
            foreach (FieldInfo fieldInfo in fields)
            {
                FieldInfo field = type2.GetField(fieldInfo.Name, flags);
                if (field != null && fieldInfo.FieldType == field.FieldType)
                    fieldInfo.SetValue(dst, field.GetValue(src));
            }
        }

        public void SetObject(object obj)
        {
            if (obj.GetType() != this.InnerType)
                return;
            this._obj = obj;
        }

        public object PrivateStaticField(string fieldName)
        {
            return this._GetFiled(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.GetField);
        }

        public T PrivateStaticField<T>(string fieldName) where T : class
        {
            return this.PrivateStaticField(fieldName) as T;
        }

        public object PrivateInstanceField(string fieldName)
        {
            return this._GetFiled(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
        }

        public T PrivateInstanceField<T>(string fieldName) where T : class
        {
            return this.PrivateInstanceField(fieldName) as T;
        }

        public object PublicInstanceField(string fieldName)
        {
            return this._GetFiled(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField);
        }

        public T PublicInstanceField<T>(string fieldName) where T : class
        {
            return this.PublicInstanceField(fieldName) as T;
        }

        private object _GetFiled(string fieldName, BindingFlags flags)
        {
            if (null == this.InnerType)
                return (object) null;
            FieldInfo field = this.InnerType.GetField(fieldName, flags);
            if (null == field)
                return (object) null;
            return field.GetValue(this._obj);
        }

        public object CallPublicInstanceMethod(string methodName, params object[] args)
        {
            return this._InvokeMethod(methodName, BindingFlags.Instance | BindingFlags.Public, args);
        }

        public object CallPublicStaticMethod(string methodName, params object[] args)
        {
            return this._InvokeMethod(methodName, BindingFlags.Static | BindingFlags.Public, args);
        }

        public object _InvokeMethod(string methodName, BindingFlags flags, params object[] args)
        {
            if (null == this.InnerType)
                return null;
            MethodInfo method = this.InnerType.GetMethod(methodName, flags);
            if (null == method)
                return null;
            return method.Invoke(this._obj, args);
        }
    }

    public class DynamicType
    {
        private Assembly _assembly;

        public DynamicType(Type type)
        {
            this._assembly = type.Assembly;
        }

        public Dynamic GetType(string typeName)
        {
            return new Dynamic(this._assembly.GetType(typeName));
        }

    }
}

