//////////////////////////////////////////////////////////////////////////
/// @file	Element.cs
///
/// @author Colin Nickerson
///
/// @brief	A unique attribute (GUID) that can be added to any component.
///
/// @note 	Copyright 2016 Hutch Games Ltd. All rights reserved.
//////////////////////////////////////////////////////////////////////////

/************************ EXTERNAL NAMESPACES ***************************/

using UnityEngine;                                                              // Unity 			(ref http://docs.unity3d.com/Documentation/ScriptReference/index.html)
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;

namespace HCP
{
    //////////////////////////////////////////////////////////////////////////
    /// @brief	Element class.  Stores a guid for the component it is 
    /// attached to.  This isn't editable.  HCP requires objects to have Element
    /// components if they wish to be visible to it.
    //////////////////////////////////////////////////////////////////////////
    public static class Element 
    {
		public static string ReflectProperty(Component element, string propertyName)
		{
			string value = "";

			try
			{
				value = element.GetType().GetProperty(propertyName).GetValue(element, null).ToString();
			}
			catch(Exception e)
			{
				value = e.ToString();
			}
			
			return value;
		}
		
		public static string ReflectMethod0(Component element, string methodName)
		{
			string value = "";

			try
			{
				value = element.GetType().GetMethod(methodName).Invoke(element, null).ToString();
			}
			catch(Exception e)
			{
				value = e.ToString();
			}
			
			return value;
		}

		public static string ConstructXPath(Component element)
		{
			string path = "/" + element.name;
			var a = element.transform.parent;
			while (a != null) { path = "/" + a.name + path; a = a.transform.parent; }

			if(element is UnityEngine.Transform == false)
			{
				path =  path + "|";

				string elementTypeName = element.GetType().Name;
				if(elementTypeName.Equals(element.name) == false)
				{
					path =  path + elementTypeName;
				}
			}
			return path;
		}

		public static Rect ConstructScreenRect(Component element) 
		{
			Vector3[] screenCorners = new Vector3[2];
			Vector3[] worldCorners = new Vector3[2];
			Camera camera = null;

			var rectTransform = element.GetComponent<RectTransform>();
			var renderer = element.GetComponent<Renderer>();

			if(rectTransform != null)
				// UI Elements
			{
				var canvas = element.GetComponentInParent<Canvas>();
				if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
				{
					camera = canvas.worldCamera;
				}
				else
				{
					camera = null;
				}
				
				var corners = new Vector3[4];
				rectTransform.GetWorldCorners(corners);

				worldCorners[0] = corners[1];
				worldCorners[1] = corners[3];
			}
			else if(renderer)
				// Renderables
			{
				worldCorners[0] = renderer.bounds.min;
				worldCorners[1] = renderer.bounds.max;
			}
			else
				// Non-renderable
			{
				worldCorners[0] = element.transform.position;
				worldCorners[1] = element.transform.position;
			}

			// Calculate screen rect
			screenCorners[0] = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[0]);
			screenCorners[1] = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[1]);
			
			screenCorners[0].y = Screen.height - screenCorners[0].y;
			screenCorners[1].y = Screen.height - screenCorners[1].y;
			
			return new Rect(screenCorners[0], screenCorners[1] - screenCorners[0]);
	    }

		public static string GetName(Component element) 
		{
			return element.GetType().Name;
		}

		public static string GetClassName(Component element) 
		{
			return element.GetType().FullName;
		}

		public static bool DetermineDisplayed(Component element) 
		{
			UnityEngine.Rect rect = Element.ConstructScreenRect(element);

            if (rect.y < 0 || rect.y + rect.y > UnityEngine.Screen.height ||
                rect.x < 0 || rect.x + rect.x > UnityEngine.Screen.width)
            {
                return false;
            }
            else
            {
                return element.gameObject.activeInHierarchy;
            }
		}

		public static bool GetEnabled(Component element) 
		{
			var behaviour = element as UnityEngine.Behaviour;
            return (behaviour == null || behaviour.enabled);
		}

		public static bool GetSelected(Component element) 
		{
			return (element.gameObject == EventSystem.current.currentSelectedGameObject);
		}
	}

	public static class ElementId
	{
		public static string ObjectPart(string id)
		{
			var groups = Regex.Match(id, "([^|]+)").Groups;
			return groups.Count > 1 ? groups[1].Value : null;
		}

		/// <summary>
		/// Returns the component part of the id.  Its important to note that
		/// the component part will be generated under the following scenarios:
		/// 1) Component part is specified, but empty (<ObjectPart>:) 
		/// In this case the component part will equal the object part and thus
		/// looks for a component of the same name as the object
		/// 2) Component part is not specified
		/// In this case the component part becomes Transform
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public static string ComponentPart(string id)
		{
			var groups = Regex.Match(id, ".*|([^\\[]+)").Groups;
			return groups.Count > 1 ? groups[1].Value : (HasComponentPart(id) ? ObjectPart(id) : "Transform");
		}

		public static bool IsSticky(string id)
		{
			return id.Contains("-"); // Is a guid
		}

		public static bool HasComponentPart(string id)
		{
			return id.Contains("|"); // component separator
		}
    }
}

