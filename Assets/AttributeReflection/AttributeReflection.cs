﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// https://www.zhihu.com/question/37329635/answer/71500828
//Type类的方法：
	//GetConstructor(), GetConstructors()：返回ConstructorInfo类型，用于取得该类的构造函数的信息；
	//GetEvent(), GetEvents()：返回EventInfo类型，用于取得该类的事件的信息；
	//GetField(), GetFields()：返回FieldInfo类型，用于取得该类的字段（成员变量）的信息；
	//GetInterface(), GetInterfaces()：返回InterfaceInfo类型，用于取得该类实现的接口的信息；
	//GetMember(), GetMembers()：返回MemberInfo类型，用于取得该类的所有成员的信息；
	//GetMethod(), GetMethods()：返回MethodInfo类型，用于取得该类的方法的信息；
	//GetProperty(), GetProperties()：返回PropertyInfo类型，用于取得该类的属性的信息。

/// <summary>
/// 事件属性
/// </summary>
public class EventMethod : Attribute
{
	/// <summary>
	/// 事件名
	/// </summary>
	public string EventName
	{
		get;
		set;
	}

	/// <summary>
	/// GameObjectName
	/// </summary>
	public string GameObjectName
	{
		get;
		set;
	}

	public Type ComponentType;

	public EventMethod(string eventName, string gameObjectName, Type componentType)
	{
		this.EventName = eventName;
		this.GameObjectName = gameObjectName;
		this.ComponentType = componentType;
	}
}

public class Reflection : MonoBehaviour
{
	/// <summary>
	/// PropertyInfo
	/// </summary>
	public bool yyyyyyyyyyyyyyyyyyyyyyyy
	{
		get;
		set;
	}

	public Button.ButtonClickedEvent ButtonClick;
	public Event EventTest;
	public Action ActionTest;
	public Func<int> FuncTest;

	public PropertyInfo[] _bastAttr;
	private PropertyInfo[] _thisAttr;

	public Reflection()
	{
	}

	~Reflection()
	{

	}

	private void Awake()
	{
		//PrintMember();
		//PrintAttr();
		//PrintEvent();
		//PrintMethod();
		RegistClickEvent();
	}

	private void RegistClickEvent()
	{
		var thisMethodInfo = this.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		var thisMemberInfo = this.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		foreach (var item in thisMethodInfo)
		{
			if(item.ReflectedType != item.DeclaringType)
			{
				continue;
			}
			//Debug.Log(item.Name + "  " + item.Attributes); // 打印OnClick_Button  Public, HideBySig
			var objList = item.GetCustomAttributes(typeof(EventMethod), false);
			foreach (var attr in objList)
			{
				// https://www.cnblogs.com/mq0036/p/7844369.html
				if (attr is EventMethod)
                 {
					EventMethod exe = attr as EventMethod;
					Debug.Log("Click Event " + item.Name + "  EventName:" + exe.EventName + " GameObjectName:" + exe.GameObjectName);
					var button = GetChildComponent(exe.GameObjectName, exe.ComponentType);
					Debug.Log(button);
					MemberInfo[] a= button.GetType().GetMember(exe.EventName);
					//methodInfo.Invoke(reflectTest, new object[] { "测试" })
					int count = 0;
					foreach(var b in a)
					{
						Debug.Log(count++ + "\nName: " + b.Name + "\n ReflectedType:" + b.ReflectedType
				+ "\n DeclaringType:" + b.DeclaringType + "\n GetType:" + b.GetType() + "\n MemberType:" + b.MemberType + "\n ToString:" + b.ToString());
					}

					var field = button.GetType().GetField(exe.EventName);
					Debug.Log(field.GetType() + "   " + exe.EventName);
				}
			}
			//if (item.GetCustomAttributes(typeof(EventMethod), true))
			//{
			//	continue;
			//}
		}
	}

	public Component GetChildComponent(string name, Type type)
	{
		GameObject obj = GetChild(name);
		if(obj != null)
		{
			return obj.GetComponent(type);
		}
		else
		{
			return null;
		}
	}

	public GameObject GetChild(string name)
	{
		return GetChild(this.gameObject, name);
	}

	public GameObject GetChild(GameObject root, string name)
	{
		if(root == null)
		{
			return null;
		}

		if(string.IsNullOrEmpty(name))
		{
			return null;
		}

		if(root.name == name)
		{
			return root;
		}

		foreach(Transform child in root.transform)
		{
			var target = GetChild(child.gameObject, name);
			if(target != null)
			{
				return target;
			}
		}
		return null;
	}

	// ReflectedType 现在反射的类型
	// DeclaringType 定义的类型
	private void PrintMember()
	{
		var bastMembers = typeof(Reflection).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static); // 只能找到get/set方法
		var _thisMembers = this.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		Debug.LogError("-----------------------Reflection Member Begin-----------------------");
		int count = 0;
		foreach (var item in bastMembers)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------Reflection Member End-----------------------");

		Debug.LogError("-----------------------GameObject Member Begin-----------------------");
		Debug.Log(this.GetType());
		count = 0;
		foreach (var item in _thisMembers)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------GameObject Member End-----------------------");
	}


	private void PrintAttr()
	{
		_bastAttr = typeof(Button).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static); // 只能找到get/set方法
		_thisAttr = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		Debug.LogError("-----------------------Reflection Attr Begin-----------------------");
		int count = 0;
		foreach (var item in _bastAttr)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n PropertyType: " + item.PropertyType + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------Reflection Attr End-----------------------");

		Debug.LogError("-----------------------GameObject Attr Begin-----------------------");
		Debug.Log(this.GetType());
		count = 0;
		foreach (var item in _bastAttr)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n PropertyType: " + item.PropertyType + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------GameObject Attr End-----------------------");
	}

	private void PrintEvent()
	{
		EventInfo[] baseEventInfo = typeof(Reflection).GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static); 
		EventInfo[] thisEventInfo = this.GetType().GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		Debug.LogError("-----------------------Reflection Event Begin-----------------------");
		int count = 0;
		foreach (var item in baseEventInfo)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------Reflection Event End-----------------------");

		Debug.LogError("-----------------------GameObject Event Begin-----------------------");
		Debug.Log(this.GetType());
		count = 0;
		foreach (var item in thisEventInfo)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------GameObject Event End-----------------------");
	}

	private void PrintMethod()
	{
		MethodInfo[] baseMethodInfo = typeof(Reflection).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		Debug.LogError("-----------------------Reflection Method Begin-----------------------");
		int count = 0;
		foreach (var item in baseMethodInfo)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------Reflection Method End-----------------------");

		MethodInfo[] thisMethodInfo = this.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		Debug.LogError("-----------------------GameObject Method Begin-----------------------");
		Debug.Log(this.GetType());
		count = 0;
		foreach (var item in thisMethodInfo)
		{
			Debug.Log(count++ + "\nName: " + item.Name + "\n ReflectedType:" + item.ReflectedType
				+ "\n DeclaringType:" + item.DeclaringType + "\n GetType:" + item.GetType() + "\n MemberType:" + item.MemberType + "\n ToString:" + item.ToString());
		}
		Debug.LogError("-----------------------GameObject Method End-----------------------");
	}
}

public class AttributeReflection : Reflection
{
	public float xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx;

	public delegate void SupportedProtocolClientEventHandler();

	// EventInfo https://blog.csdn.net/wf751620780/article/details/78592494
	public event SupportedProtocolClientEventHandler SupportedProtocolEvent;

	[EventMethod("onClick", "Button", typeof(Button))]
	public void OnClick_Button()
	{
		Debug.Log("Attribute Reflection");
	}
}
