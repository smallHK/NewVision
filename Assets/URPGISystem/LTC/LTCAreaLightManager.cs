using System.Collections.Generic;
using UnityEngine;

namespace NewVision.LTC
{
    /// <summary>
    /// LTC区域光管理器
    /// 负责管理场景中的所有LTC区域光
    /// </summary>
    public class LTCAreaLightManager
    {
        /// <summary>
        /// 场景中的区域光集合
        /// </summary>
        private static HashSet<LTCAreaLight> s_AreaLights = new HashSet<LTCAreaLight>();

        /// <summary>
        /// 注册区域光
        /// </summary>
        /// <param name="areaLight">要注册的区域光</param>
        public static void Register(LTCAreaLight areaLight)
        {
            if (areaLight != null)
            {
                s_AreaLights.Add(areaLight);
            }
        }

        /// <summary>
        /// 注销区域光
        /// </summary>
        /// <param name="areaLight">要注销的区域光</param>
        public static void Unregister(LTCAreaLight areaLight)
        {
            if (areaLight != null)
            {
                s_AreaLights.Remove(areaLight);
            }
        }

        /// <summary>
        /// 获取所有区域光
        /// </summary>
        /// <returns>区域光集合</returns>
        public static HashSet<LTCAreaLight> Get()
        {
            return s_AreaLights;
        }

        /// <summary>
        /// 获取区域光数量
        /// </summary>
        /// <returns>区域光数量</returns>
        public static int GetCount()
        {
            return s_AreaLights.Count;
        }
    }
}