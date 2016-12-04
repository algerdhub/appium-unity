//////////////////////////////////////////////////////////////////////////
/// @file	Sticky.cs
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

namespace HCP
{
    //////////////////////////////////////////////////////////////////////////
    /// @brief	StickyAttribute class.  
    //////////////////////////////////////////////////////////////////////////
    public class StickyAttribute : PropertyAttribute {}

    //////////////////////////////////////////////////////////////////////////
    /// @brief	Sticky class.  Stores a guid for the component it is 
    /// attached to.  This isn't editable.  HCP can use this to find
	/// "sticky elements" that you want to have persistable ids across builds
    //////////////////////////////////////////////////////////////////////////
    [AddComponentMenu("HCP/Element")]
    public abstract class Sticky : MonoBehaviour
    {
        /***************************** PUBLIC DATA ******************************/
        [Sticky]
        [SerializeField]
        protected string m_sUniqueGuid;
        public string Id { get { return "HCP-" + (this.m_bUnsafe ? "UNSAFE-" : "" ) + m_sUniqueGuid; } }

        [SerializeField]
        [HideInInspector]
        protected bool m_bUnsafe = true;   // Generated at runtime
				
        [Sticky]
        [NonSerialized]
        protected string m_sPath;
        public string Path
		{
			get
			{
				this.CalculatePath();
				return m_sPath;
			}
		}

		[Sticky]
        [NonSerialized]
        protected string m_sName;
        public string Name
		{
			get
			{
				this.CalculateName();
				return m_sName;
			}
		}

        // Reset is called when the user hits the Reset button in the Inspector's 
        // context menu or when adding the component the first time. This function
        // is only called in editor mode. Reset is most commonly used to give good 
        // default values in the inspector.
        private void Reset()
        {
            m_bUnsafe = false;     
            
            GenerateUId();
			CalculatePath();	// Assumes the object doesn't move around
			CalculateName();	// Assumes the object name does not change
        }

        private void GenerateUId()
        {
            // Generate a unique ID, defaults to an empty string if nothing has been serialized yet
            if (String.IsNullOrEmpty(m_sUniqueGuid))
            {
                Guid guid = Guid.NewGuid();
                m_sUniqueGuid = guid.ToString();
            }
        }
		
		private void CalculatePath()
			// example: /one/two/three
		{
			m_sPath = Element.ConstructXPath(this.transform);
		}

		private void CalculateName()
		{
			m_sName = this.name;
		}
		
        private void Start()
        {
            if(this.GetComponents<Sticky>().Length > 1)
            {
                throw new System.Exception("HCP.Sticky Error - You cannot attach more than one Sticky component to a single game object.");
            }
            
            GenerateUId();
			CalculatePath();	// Assumes the object doesn't move around
			CalculateName();	// Assumes the object name does not change
        }
    }
}

