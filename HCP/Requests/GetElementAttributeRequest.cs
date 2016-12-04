using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HCP;
using HCP.SimpleJSON;
using UnityEngine.EventSystems;

namespace HCP.Requests
{
    public class GetElementAttributeRequest : JobRequest
    {
        public enum EAttribute
        {
            NAME,
            CLASSNAME,
            DISPLAYED,
            ENABLED,
            SELECTED
        };

        public string Id { get { return Data["elementId"]; } }
        public EAttribute Attribute
        {
            get
            {
                var stringValue = Data["attribute"];

                switch (stringValue)
                {
                    case "name": return EAttribute.NAME;
                    case "className": return EAttribute.CLASSNAME;
                    case "displayed": return EAttribute.DISPLAYED;
                    case "enabled": return EAttribute.ENABLED;
                    case "selected": return EAttribute.SELECTED;

                    default: throw new FormatException("Unsupported element request");
                }
            }
        }


        public GetElementAttributeRequest(JSONClass json) : base(json)
        {
        }
		

        public override JobResponse Process()
        {
            string response = null;
            var element = JobRequest.GetElementById(this.Id);

            switch (this.Attribute)
            {
                case EAttribute.NAME:
                    response = Element.GetName(element);
                    break;
                case EAttribute.CLASSNAME:
                    response = Element.GetClassName(element);
                    break;
                case EAttribute.DISPLAYED:
					response = Element.DetermineDisplayed(element) ? "true" : "false";
					break;
                case EAttribute.ENABLED:
					response = Element.GetEnabled(element) ? "true" : "false";
                    break;
                case EAttribute.SELECTED:
					response = Element.GetSelected(element) ? "true" : "false";
                    break;
            }

            return new Responses.StringResponse(response);
        }
    }
}
