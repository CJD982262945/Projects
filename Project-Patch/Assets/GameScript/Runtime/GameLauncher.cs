﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MotionFramework;
using MotionFramework.Console;
using MotionFramework.Resource;
using MotionFramework.Event;
using MotionFramework.Config;
using MotionFramework.Audio;
using MotionFramework.Network;
using MotionFramework.Patch;
using MotionFramework.Scene;
using MotionFramework.Pool;

public class GameLauncher : MonoBehaviour
{
	public bool SimulationOnEditor = false;
	public bool SkipCDN = false;

	void Awake()
	{
#if !UNITY_EDITOR
		SimulationOnEditor = false;
#endif

		// 初始化应用
		InitAppliaction();

		// 初始化框架
		bool showConsole = Application.isEditor || Debug.isDebugBuild;
		MotionEngine.Initialize(this, showConsole, HandleMotionFrameworkLog);
	}
	void Start()
	{
		// 创建游戏模块
		CreateGameModules();
	}
	void Update()
	{
		// 更新框架
		MotionEngine.Update();
	}
	void OnGUI()
	{
		// 绘制控制台
		MotionEngine.DrawConsole();
	}

	/// <summary>
	/// 初始化应用
	/// </summary>
	private void InitAppliaction()
	{
		Application.runInBackground = true;
		Application.backgroundLoadingPriority = ThreadPriority.High;

		// 设置最大帧数
		Application.targetFrameRate = 60;

		// 屏幕不休眠
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
	}

	/// <summary>
	/// 监听框架日志
	/// </summary>
	private void HandleMotionFrameworkLog(ELogLevel logLevel, string log)
	{
		if (logLevel == ELogLevel.Log)
		{
			UnityEngine.Debug.Log(log);
		}
		else if (logLevel == ELogLevel.Error)
		{
			UnityEngine.Debug.LogError(log);
		}
		else if (logLevel == ELogLevel.Warning)
		{
			UnityEngine.Debug.LogWarning(log);
		}
		else if (logLevel == ELogLevel.Exception)
		{
			UnityEngine.Debug.LogError(log);
		}
		else
		{
			throw new NotImplementedException($"{logLevel}");
		}
	}

	/// <summary>
	/// 创建游戏模块
	/// </summary>
	private void CreateGameModules()
	{
		// 创建事件管理器
		MotionEngine.CreateModule<EventManager>();
	
		IBundleServices bundleServices;
		if (SkipCDN)
		{
			var variantRule1 = new VariantRule();
			variantRule1.VariantGroup = new List<string>() { "CN", "EN", "KR" };
			variantRule1.TargetVariant = "EN";
			var variantRules = new List<VariantRule>() { variantRule1 };
			bundleServices = new LocalBundleServices(variantRules);
		}
		else
		{
			// 创建补丁管理器
			string myWebServer = "127.0.0.1";
			string myCDNServer = "127.0.0.1";

			var patchCreateParam = new PatchManager.CreateParameters();
			patchCreateParam.ServerID = PlayerPrefs.GetInt("SERVER_ID_KEY", 0);
			patchCreateParam.ChannelID = 0;
			patchCreateParam.DeviceID = 0;
			patchCreateParam.TestFlag = PlayerPrefs.GetInt("TEST_FLAG_KEY", 0);
			patchCreateParam.CheckLevel = ECheckLevel.CheckSize;

			patchCreateParam.WebServers = new Dictionary<RuntimePlatform, string>();
			patchCreateParam.WebServers.Add(RuntimePlatform.Android, $"{myWebServer}/WEB/Android/GameVersion.php");
			patchCreateParam.WebServers.Add(RuntimePlatform.IPhonePlayer, $"{myWebServer}/WEB/Iphone/GameVersion.php");

			patchCreateParam.CDNServers = new Dictionary<RuntimePlatform, string>();
			patchCreateParam.CDNServers.Add(RuntimePlatform.Android, $"{myCDNServer}/CDN/Android");
			patchCreateParam.CDNServers.Add(RuntimePlatform.IPhonePlayer, $"{myCDNServer}/CDN/Iphone");

			patchCreateParam.DefaultWebServerIP = $"{myWebServer}/WEB/PC/GameVersion.php";
			patchCreateParam.DefaultCDNServerIP = $"{myCDNServer}/CDN/PC";

			var variantRule1 = new VariantRule();
			variantRule1.VariantGroup = new List<string>() { "CN", "EN", "KR" };
			variantRule1.TargetVariant = "EN";
			patchCreateParam.VariantRules = new List<VariantRule>() { variantRule1 };

			MotionEngine.CreateModule<PatchManager>(patchCreateParam);
			bundleServices = MotionEngine.GetModule<PatchManager>();
			EventManager.Instance.AddListener<PatchEventMessageDefine.PatchStatesChange>(OnHandleEventMessage);
			EventManager.Instance.AddListener<PatchEventMessageDefine.OperationEvent>(OnHandleEventMessage);
		}

		// 创建资源管理器
		var resourceCreateParam = new ResourceManager.CreateParameters();
		resourceCreateParam.LocationRoot = GameDefine.AssetRootPath;
		resourceCreateParam.SimulationOnEditor = SimulationOnEditor;
		resourceCreateParam.BundleServices = bundleServices;
		resourceCreateParam.DecryptServices = null;
		resourceCreateParam.AutoReleaseInterval = 10f;
		MotionEngine.CreateModule<ResourceManager>(resourceCreateParam);

		if (SkipCDN)
		{
			// 开始游戏
			StartGame();
		}
		else
		{
			// 开始补丁更新流程
			PatchManager.Instance.Run();
		}
	}

	/// <summary>
	/// 事件处理
	/// </summary>
	private void OnHandleEventMessage(IEventMessage msg)
	{
		if (msg is PatchEventMessageDefine.PatchStatesChange)
		{
			var message = msg as PatchEventMessageDefine.PatchStatesChange;

			// 初始化结束
			if (message.CurrentStates == EPatchStates.InitiationOver)
			{
				PatchWindow.Instance.Initialize();
			}

			// 补丁下载完毕
			// 注意：在补丁下载结束之后，一定要强制释放资源管理器里所有的资源，还有重新载入Unity清单。
			if (message.CurrentStates == EPatchStates.DownloadOver)
			{
				PatchWindow.Instance.Destroy();
				ResourceManager.Instance.ForceReleaseAll();
				PatchManager.Instance.ReloadUnityManifest();

				// 开始游戏
				StartGame();
			}
		}
		else if (msg is PatchEventMessageDefine.OperationEvent)
		{
			PatchManager.Instance.HandleEventMessage(msg);
		}
	}

	private void StartGame()
	{
		MotionEngine.CreateModule<Demo>();
		Demo.Instance.StartGame();
	}
}