using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HCP;
using HCP.SimpleJSON;

using UnityEngine;

namespace HCP.Requests
{
    public class GetElementSizeRequest : JobRequest
    {
        public string Id { get { return Data ["elementId"]; } }

        public GetElementSizeRequest (JSONClass json) : base (json)
        {
        }
        public override JobResponse Process ()
        {
            var element = JobRequest.GetElementById (this.Id);
			var rect = Element.ConstructScreenRect(element);
            Vector3 size = new Vector3()
			{
				x = rect.width,
				y = rect.height,
				z = 0
			};

            return Responses.JSONResponse.FromObject (new { width = (int)size.x, height = (int)size.y, depth = (int)size.z });
            // Note that appium has no concept of depth, but passing it anyways
        }
    }
}
