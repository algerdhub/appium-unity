using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HCP;
using HCP.SimpleJSON;
using UnityEngine.EventSystems;

namespace HCP.Requests
{
    public class ElementReflectionRequest : JobRequest
    {
        public enum ETarget
        {
            PROPERTY,
            METHOD0,
        };

        public string Id { get { return Data["elementId"]; } }
		public string Name { get { return Data["name"]; } }
        public ETarget Target
        {
            get
            {
                var stringValue = Data["attribute"];

                switch (stringValue)
                {
                    case "property": return ETarget.PROPERTY;
                    case "method0": return ETarget.METHOD0;

                    default: throw new FormatException("Unsupported element request");
                }
            }
        }


        public ElementReflectionRequest(JSONClass json) : base(json)
        {
        }
		
        public override JobResponse Process()
        {
            string response = null;
            var element = JobRequest.GetElementById(this.Id);

            switch (this.Target)
            {
                case ETarget.PROPERTY:
                    response = Element.ReflectProperty(element, this.Name);
                    break;
                case ETarget.METHOD0:
                    response = Element.ReflectMethod0(element, this.Name);
                    break;
            }

            return new Responses.StringResponse(response);
        }
    }
}
