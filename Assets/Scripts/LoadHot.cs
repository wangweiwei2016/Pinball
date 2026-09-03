using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DefaultNamesapce
{
    public class LoadHot : MonoBehaviour
    {
        private void Start()
        {
            LoadAssembly();
        }

        private void LoadAssembly()
        {
            // 1. 加载热更新DLL（直接从StreamingAssets读取）
            string dllPath = Path.Combine(Application.streamingAssetsPath, "HotUpdate.dll.bytes");
            
            if (!File.Exists(dllPath))
            {
                Debug.LogError($"热更新DLL不存在: {dllPath}");
                return;
            }

            // 读取DLL字节数据
            byte[] dllBytes = File.ReadAllBytes(dllPath);
            
            // 2. 加载程序集到当前AppDomain
            Assembly hotupdateAss = Assembly.Load(dllBytes);
            
            // 3. 验证是否加载成功
            if (hotupdateAss == null)
            {
                Debug.LogError("程序集加载失败");
                return;
            }
            
            Debug.Log($"成功加载程序集: {hotupdateAss.GetName().Name}");
            
            // 4. 获取HotUpdateMain类型
            Type type = hotupdateAss.GetType("HotUpdate.HotUpdateMain"); //HotUpdateMain类是在HotUpdate命名空间下的
            
            if (type == null)
            {
                // 如果找不到，打印所有类型帮助排查
                Debug.LogError("找不到 HotUpdateMain 类型，已加载的类型列表:");
                foreach (var t in hotupdateAss.GetTypes())
                {
                    Debug.Log($"  - {t.FullName}");
                }
                return;
            }
            
            // 5. 调用HotUpdateMain.Run方法
            MethodInfo runMethod = type.GetMethod("Run");
            
            if (runMethod == null)
            {
                Debug.LogError("找不到 Run 方法");
                return;
            }
            
            // 执行热更新入口
            runMethod.Invoke(null, null);
            
            Debug.Log("热更新启动成功");
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                LoadAssembly();
            }
        }
    }
}