using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HCP;
using HCP.SimpleJSON;
using System.Xml;

using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Text.RegularExpressions;

namespace HCP.Requests
{
    // Format sample at EOF
    public class PageSourceRequest : JobRequest
    {
        public PageSourceRequest(JSONClass json) : base(json)
        {
        }

		protected static string ConstructElementId(GameObject g)
		{
			var sticky = g.GetComponent<Sticky>();
			if (sticky)
				return sticky.Id;
			else
				return "";
		}

		protected static string FormatXMLNodeName(string name)
		{
			string pattern = "([\\(\\)\\s])";
			string replacement = ".";
			Regex rgx = new Regex(pattern);
			string result = rgx.Replace(name, replacement);
			
			return result;
		}

		/// <summary>
		/// A list of which types are excluded from interets when building
		/// a pagesource.  A gameobject is added to pagesource if it has
		/// something of interest.  By default we think every type is
		/// interesting
		/// </summary>
		protected static List<Type> s_uninterestingTypes = new List<Type>()
		{
			typeof(UnityEngine.Transform),
			typeof(UnityEngine.RectTransform),
			typeof(UnityEngine.Renderer),
		};

		/// <summary>
		/// A list of which namespaces are included interets when building
		/// a pagesource.  Types which live outside of these namespace names
		/// are not interesting
		/// </summary>
		protected static List<string> s_namespaceInterests = new List<string>()
		{
			"UnityEngine.UI",
			null
		};
		
		/// <summary>
		/// All the types that are clickable.  Types not in this list ignore
		/// mouse presses in other tools which use this data.
		/// </summary>
		protected static List<Type> s_clickableTypes = new List<Type>()
		{
			typeof(UnityEngine.UI.Button),
			typeof(UnityEngine.UI.InputField),
			typeof(UnityEngine.UI.Toggle)
		};

		/// <summary>
		/// All the types that are toggleable.  Types not in this list ignore
		/// mouse presses in other tools which use this data.
		/// </summary>
		protected static List<Type> s_checkableTypes = new List<Type>()
		{
			typeof(UnityEngine.UI.Toggle)
		};


		/// <summary>
		/// </summary>
		/// <param name="gameObject"></param>
		/// <param name="xmlDoc"></param>
		/// <param name="parentXmlElement"></param>
		/// <param name="index"></param>
        protected static void CompleteComponent(Component component, XmlDocument xmlDoc, XmlElement parentXmlElement)
        {
			// Incomplete - meant to be used to replicate the unity display where each component is its own
			// item in the tree

			/*
			var xmlElement = parentXmlElement;

			var childXmlElement = xmlDoc.CreateElement (Element.GetName(component));
			childXmlElement.SetAttribute ("class", Element.GetClassName(component));


			// General attributes
			childXmlElement.SetAttribute("package", "UnityEngine.Component");
            childXmlElement.SetAttribute("isHCP", "true");
			childXmlElement.SetAttribute("name", Element.GetName(component));
			childXmlElement.SetAttribute("resource-id", JobRequest.ConstructElementId(component));
			childXmlElement.SetAttribute("enabled", Element.GetEnabled(component) ? "true" : "false");
			childXmlElement.SetAttribute("displayed", Element.GetDisplayed(component) ? "true" : "false");
			*/			
		}

		/// <summary>
		/// Recursively goes through a gameobject and adds it to the tree if it has one or more components that are
		/// interesting
		/// </summary>
		/// <param name="gameObject"></param>
		/// <param name="xmlDoc"></param>
		/// <param name="parentXmlElement"></param>
		/// <param name="index"></param>
        protected static void CompleteChild(GameObject gameObject, XmlDocument xmlDoc, XmlElement parentXmlElement, int index)
        {
			var xmlElement = parentXmlElement;

			var components = gameObject.GetComponents<Component>();
			var componentTypes = components.Select(c => c.GetType());
			var transform = gameObject.GetComponent<Transform>();
			
			var interests = components
					.Where(c => s_namespaceInterests.Contains(c.GetType().Namespace) && s_uninterestingTypes.Contains(c.GetType()) == false);
            
			if(interests.Any())
				// This gameObject has some interesting stuff
			{
				var childXmlElement = xmlDoc.CreateElement (FormatXMLNodeName(gameObject.name));

				// General attributes
				childXmlElement.SetAttribute("class", gameObject.GetType().FullName);
				childXmlElement.SetAttribute("package", "UnityEngine.GameObject");
				childXmlElement.SetAttribute("isHCP", "true");
				childXmlElement.SetAttribute("name", gameObject.name);
				childXmlElement.SetAttribute("path", Element.ConstructXPath(transform));
				childXmlElement.SetAttribute("index", index.ToString());
				childXmlElement.SetAttribute("resource-id", PageSourceRequest.ConstructElementId(gameObject));
				childXmlElement.SetAttribute("enabled", gameObject.activeSelf ? "true" : "false");
				childXmlElement.SetAttribute("displayed", gameObject.activeInHierarchy ? "true" : "false");

				// Bounds
				var rect = Element.ConstructScreenRect(transform);
				childXmlElement.SetAttribute("bounds", String.Format("[{0},{1}][{2},{3}]", (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
           			
				// Clickable?
				childXmlElement.SetAttribute ("clickable", componentTypes.Any(t => s_clickableTypes.Contains(t)) ? "true" : "false");
			
				// Checkable?
				childXmlElement.SetAttribute ("checkable", componentTypes.Any(t => s_clickableTypes.Contains(t)) ? "true" : "false");

				// Complete components list (for future use)
				childXmlElement.SetAttribute("components", 
					interests
						.Select(c => Element.GetName(c))
						.ToString());
				

				parentXmlElement.AppendChild(childXmlElement);
                xmlElement = childXmlElement;
				

				// Walk components (not implemented)
				interests
					.ToList()
					.ForEach(c => CompleteComponent(c, xmlDoc, childXmlElement));
			}

			// Walk gameObjects
            for(int i = 0; i < gameObject.transform.childCount; i++)
            {
                var child = gameObject.transform.GetChild(i);
                CompleteChild(child.gameObject, xmlDoc, xmlElement, i);
            }
        }

        public override JobResponse Process()
        {
			// Create xml doc to hold page source of UI hierarchy
            XmlDocument xmlDoc = new XmlDocument( );

            XmlElement xmlElement = xmlDoc.CreateElement("hierarchy");
            xmlDoc.AppendChild(xmlElement);

            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            for(int i = 0; i < roots.Length; i++)
            {
                CompleteChild(roots[i].gameObject, xmlDoc, xmlElement, i);
            }
            
            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = XmlWriter.Create(stringWriter))
            {
                xmlDoc.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                return new Responses.StringResponse(stringWriter.GetStringBuilder().ToString());
            }			
        }
    }
}

//<?xml version="1.0" encoding="UTF-8"?>
//<hierarchy rotation="0">
//	<android.widget.FrameLayout index="0" text="" class="android.widget.FrameLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][1920,1080]" resource-id="" instance="0">
//		<android.widget.LinearLayout index="0" text="" class="android.widget.LinearLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,0][1920,1080]" resource-id="" instance="0">
//			<android.widget.FrameLayout index="0" text="" class="android.widget.FrameLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,25][1920,50]" resource-id="" instance="1">
//				<android.widget.TextView index="0" text="Contact Manager" class="android.widget.TextView" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[6,26][1914,48]" resource-id="android:id/title" instance="0"/>
//			</android.widget.FrameLayout>
//			<android.widget.FrameLayout index="1" text="" class="android.widget.FrameLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,50][1920,1080]" resource-id="android:id/content" instance="2">
//				<android.widget.LinearLayout index="0" text="" class="android.widget.LinearLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,50][1920,1080]" resource-id="" instance="1">
//					<android.widget.ListView index="0" text="" class="android.widget.ListView" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" focused="true" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,50][1920,984]" resource-id="com.example.android.contactmanager:id/contactList" instance="0">
//						<android.widget.LinearLayout index="0" text="" class="android.widget.LinearLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,50][90,69]" resource-id="" instance="2">
//							<android.widget.TextView index="0" text="Gordon Wright" class="android.widget.TextView" package="com.example.android.contactmanager" content-desc="false" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,50][90,69]" resource-id="com.example.android.contactmanager:id/contactEntryText" instance="1"/>
//						</android.widget.LinearLayout>
//						<android.widget.LinearLayout index="1" text="" class="android.widget.LinearLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,70][121,89]" resource-id="" instance="3">
//							<android.widget.TextView index="0" text="jason@kotaku.com" class="android.widget.TextView" package="com.example.android.contactmanager" content-desc="false" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,70][121,89]" resource-id="com.example.android.contactmanager:id/contactEntryText" instance="2"/>
//						</android.widget.LinearLayout>
//						<android.widget.LinearLayout index="2" text="" class="android.widget.LinearLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,90][32,109]" resource-id="" instance="4">
//							<android.widget.TextView index="0" text="Mom" class="android.widget.TextView" package="com.example.android.contactmanager" content-desc="false" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,90][32,109]" resource-id="com.example.android.contactmanager:id/contactEntryText" instance="3"/>
//						</android.widget.LinearLayout>
//						<android.widget.LinearLayout index="3" text="" class="android.widget.LinearLayout" package="com.example.android.contactmanager" content-desc="" checkable="false" checked="false" clickable="true" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,110][76,129]" resource-id="" instance="5">
//							<android.widget.TextView index="0" text="Shea Martin" class="android.widget.TextView" package="com.example.android.contactmanager" content-desc="false" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,110][76,129]" resource-id="com.example.android.contactmanager:id/contactEntryText" instance="4"/>
//						</android.widget.LinearLayout>
//					</android.widget.ListView>
//					<android.widget.CheckBox index="1" text="Show Invisible Contacts (Only)" class="android.widget.CheckBox" package="com.example.android.contactmanager" content-desc="Show Invisible Contacts (Only)" checkable="true" checked="false" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,984][254,1032]" resource-id="com.example.android.contactmanager:id/showInvisible" instance="0"/>
//					<android.widget.Button index="2" text="Add Contact" class="android.widget.Button" package="com.example.android.contactmanager" content-desc="Add Contact" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" long-clickable="false" password="false" selected="false" bounds="[0,1032][1920,1080]" resource-id="com.example.android.contactmanager:id/addContactButton" instance="0"/>
//				</android.widget.LinearLayout>
//			</android.widget.FrameLayout>
//		</android.widget.LinearLayout>
//	</android.widget.FrameLayout>
//</hierarchy>
