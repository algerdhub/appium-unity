using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HCP.SimpleJSON;

using UnityEngine;

namespace HCP
{
    ////////////////////////////////////////////////////////////
    // @brief JobRequests are what they sound like.  They have 
    // JSON formated data that is used to query against. This
    // class should be derived from once for each acceptable HCP
    // action.  These actions are then registerd in the server
    // object.  See Server.cs/Awake for that process.
    ////////////////////////////////////////////////////////////
    public abstract class JobRequest
    {
        private JSONNode m_data;
        protected JSONNode Data { get { return this.m_data; } }

        public JobRequest(JSONClass json)
        {            
            if(json == null)
            {
                throw new ArgumentNullException("JobRequest cannot assign null data");
            }

            m_data = json;
        }        

        public abstract JobResponse Process();

		#region Utility
		protected static Dictionary<string, Component>	s_touchedElements = new Dictionary<string, Component>();

		protected static string ConstructElementId(Component c)
		{
			var g = c.gameObject;
			string instanceId = null;
			
			var sticky = g.GetComponent<Sticky>();
			if(sticky)
				// Overwrite id with element string
			{
				instanceId = String.Format("%0:%1", sticky.Id, c.GetType().Name);
			}
			else
			{
				instanceId = c.GetInstanceID().ToString();
			}
			
			return instanceId;
		}

		protected static void RegisterElement(string id, Component c)
		{
			if(s_touchedElements.ContainsKey(id))
				s_touchedElements[id] = c;
			else
				s_touchedElements.Add(id, c);
		}

        protected static Component GetElementById(string id)
        {
			Component e = null;

			// First look it up in our registrations
			if(s_touchedElements.ContainsKey(id))
			{
				e = s_touchedElements[id];
			}
			else
			{
				// Search
				bool isSticky = ElementId.IsSticky(id);

				if(isSticky)
				{
					var objectPart = ElementId.ObjectPart(id);
					var componentPart = ElementId.ComponentPart(id);
					var sticky = Resources.FindObjectsOfTypeAll<Sticky>().First(s => s.Id == objectPart);
					e = sticky.GetComponent(Type.GetType(componentPart));
				}
				else
				{
					var intId = int.Parse(id);
					e = Resources.FindObjectsOfTypeAll<Component>().First(c => c.GetInstanceID() == intId);
				}

				RegisterElement(id, e);
			}

			return e;
        }
        #endregion
    }
}
