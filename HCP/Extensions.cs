using UnityEngine;
using System.Collections;

namespace HCP
{
	public static class Extensions
	{
		public static T EnsureComponent<T>(this GameObject g)  where T : Component
		{
			var c = g.GetComponent<T>();
			if(c == null) c = g.AddComponent<T>();

			return c;
		}
	}
}
