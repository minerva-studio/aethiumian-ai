using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI.Utils
{
    /// <summary>
    /// Caching the member info for some reflection-based code
    /// </summary>
    public class MemberInfoCache
    {
        public class Cache
        {
            public Dictionary<string, MemberInfo> memberInfos = new();
            public Dictionary<string, MethodInfo[]> memberInfoArray = new();
        }

        public Dictionary<Type, Cache> typeCache = new();

        public static MemberInfoCache Instance { get; internal set; } = new MemberInfoCache();


        private Cache GetCache(Type type)
        {
            if (!typeCache.TryGetValue(type, out var cache))
            {
                cache = new Cache();
                typeCache[type] = cache;
            }
            return cache;
        }



        public MemberInfo GetMember(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName)) return null;
            Type type = target.GetType();
            return GetMember(type, memberName);
        }

        public MemberInfo GetMember(Type type, string memberName)
        {
            Cache cache = GetCache(type);
            if (!cache.memberInfos.TryGetValue(memberName, out var info))
            {
                MemberInfo[] memberInfos = type.GetMember(memberName);
                if (memberInfos.Length == 0) info = null;
                else info = memberInfos[0];
                cache.memberInfos[memberName] = info;
            }
            return info;
        }




        public MemberInfo GetMember(object target, string memberName, BindingFlags flags)
        {
            if (target == null || string.IsNullOrEmpty(memberName)) return null;
            Type type = target.GetType();
            return GetMember(type, memberName, flags);
        }

        public MemberInfo GetMember(Type type, string memberName, BindingFlags flags)
        {
            Cache cache = GetCache(type);
            if (!cache.memberInfos.TryGetValue(memberName, out var info))
            {
                MemberInfo[] memberInfos = type.GetMember(memberName, flags);
                if (memberInfos.Length == 0) info = null;
                else info = memberInfos[0];
                cache.memberInfos[memberName] = info;
            }
            return info;
        }




        public MethodInfo GetMethod(object target, string methodName, BindingFlags flags)
        {
            Type type = target.GetType();
            return GetMethod(type, methodName, flags);
        }

        public MethodInfo GetMethod(Type type, string methodName, BindingFlags flags)
        {
            Cache cache = GetCache(type);
            if (!cache.memberInfos.TryGetValue(methodName, out var info))
            {
                info = type.GetMethod(methodName, flags);
                cache.memberInfos[methodName] = info;
            }
            return info as MethodInfo;
        }


        public MethodInfo[] GetMethods(object target, string methodName, BindingFlags flags)
        {
            Type type = target.GetType();
            return GetMethods(type, methodName, flags);
        }

        public MethodInfo[] GetMethods(Type type, string methodName, BindingFlags flags)
        {
            Cache cache = GetCache(type);
            if (!cache.memberInfoArray.TryGetValue(methodName, out var info))
            {
                info = type.GetMethods(flags).Where(n => n.Name == methodName).ToArray();
                cache.memberInfoArray[methodName] = info;
            }
            return info;
        }
    }
}
