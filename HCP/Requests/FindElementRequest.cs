using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HCP;
using HCP.SimpleJSON;

using UnityEngine;
using System.Text.RegularExpressions;

namespace HCP.Requests
{
    public class FindElementRequest : JobRequest
    {
        public string Strategy { get { return Data["strategy"]; } }
        public string Selector { get { return Data["selector"]; } }
        public string Context { get { return Data["context"]; } }
        public bool Multiple { get { return Data["multiple"].AsBool; } }

        public FindElementRequest(JSONClass json) : base(json)
        {
        }

        protected Type GetType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(typeName);
                if (type != null) return type;
            }
            return null ;
        }
        
		

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
        protected JobResponse ProcessSingle()
        {
			GameObject gameObject = null;
			Component component = null;
            string elementId = null;
			
			// Pull out the object name
			string objectPart = ElementId.ObjectPart(this.Selector);
			
			// Pull out the component name (if there isn't one then we just want the object)
			string componentPart = ElementId.ComponentPart(this.Selector);
			

			if(String.IsNullOrEmpty(objectPart) == false)
			{
				switch (this.Strategy)
				{
					case "name":
						gameObject = Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == objectPart);
						break;
					case "id":
						gameObject = GetElementById(objectPart).gameObject;
						break;
					case "tag name":
						gameObject = Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.tag == objectPart).gameObject;
						break;
					case "class name":
						// This one is special because all gameobjects are of type gameobect, so
						// we just get the component itself
						if(ElementId.HasComponentPart(this.Selector) == true)
						{
							throw new ArgumentException("Find strategy type class name cannot have component part: " + this.Selector);
						}
						var type = this.GetType(objectPart);
						component = Resources.FindObjectsOfTypeAll<Component>().First(c => c.GetComponent(type) != null);
						break;
					case "xpath":
						// Only supports direct name pathing - path contains non-elements
						gameObject = GameObject.Find(objectPart);
						break;
					default:
						throw new ArgumentException("Find strategy type unsupported: " + this.Strategy);
				}
			}

			if(gameObject != null && component == null)
			{
				component = gameObject.GetComponent(componentPart);				
			}

			if(component != null)
			{
				elementId = JobRequest.ConstructElementId(component);
				JobRequest.RegisterElement(elementId, component);	// For later use in GetElementById calls
			}
			else
			{
				throw new ArgumentException("Could not find <" + componentPart + "> on <" + objectPart + ">");
			}

            return Responses.JSONResponse.FromObject(new { ELEMENT = elementId });
        }

        protected JobResponse ProcessMultiple()
        {
            string[] elementIds = null;
			GameObject[] gameObjects = null;
			Component[] components = null;

			// Pull out the component name (if there isn't one then we just want the object)
			string componentPart = ElementId.ComponentPart(this.Selector);

			// Pull out the object name
			string objectPart = ElementId.ObjectPart(this.Selector);

			if(String.IsNullOrEmpty(componentPart) == false) 
				throw new ArgumentException("FindElement - Multiple strategy does not currently support component selection");
            
            switch(this.Strategy)
            {
                case "name":
                    gameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == objectPart).ToArray();
                    break;
                case "id":
                    throw new ArgumentException("FindElement - You cannot find multiple of the same id");
                case "tag name":
                    gameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.tag == objectPart).ToArray();
                    break;
                case "class name":
					// This one is special because all gameobjects are of type gameobect, so
					// we just get the component itself
					if(ElementId.HasComponentPart(this.Selector) == true)
					{
						throw new ArgumentException("Find strategy type class name cannot have component part: " + this.Selector);
					}
                    var type = this.GetType(objectPart);
                    components = Resources.FindObjectsOfTypeAll<Component>().Where(c => c.GetComponent(type) != null).ToArray();
                    break;
                case "xpath":
                    throw new NotImplementedException("FindElement - Do not currently support xpath");
                default:
                    throw new ArgumentException("Find strategy type unsupported: " + this.Strategy);
            }

			if(gameObjects != null && components == null)
			{
				components = gameObjects.Where(g => g.GetComponent(componentPart) != null).Select(g => g.GetComponent(componentPart)).ToArray();	
			}
            
			if(components != null)
			{
				elementIds = components.Select(g => JobRequest.ConstructElementId(g.GetComponent(componentPart))).ToArray();
			}
			else
			{
				throw new ArgumentException("Could not find <" + componentPart + "> on <" + objectPart + ">");
			}
						
            return Responses.JSONResponse.FromArray(elementIds, (item) => { return new { ELEMENT = item }; });
        }


        public override JobResponse Process()
        {
            if(this.Multiple)
            {
                return this.ProcessMultiple();
            }
            else
            {
                return this.ProcessSingle();
            }
        }
    }
}
