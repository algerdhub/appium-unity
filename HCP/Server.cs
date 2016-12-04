//////////////////////////////////////////////////////////////////////////
/// @file	Server.cs
///
/// @author	Colin Nickersnn (CN)
///
/// @brief	Rest communication support with external tools.
///
///
/// @note 	Copyright 2016 Hutch Games Ltd. All rights reserved.
//////////////////////////////////////////////////////////////////////////

#define SEND_RESPONSE_CHUNKED																	// Send response data in 1000 byte Write chunks

/************************ EXTERNAL NAMESPACES ***************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

using UnityEngine;
using HCP.SimpleJSON;



namespace HCP
{
	public delegate void ListenerStartedEventHandler ();
	public delegate void LIstenerStoppedEventHandler ();

	//////////////////////////////////////////////////////////////////////////
	/// @brief	Server server class.  Creates a threaded HttpListener to 
	/// respond to requests to provide Unity GamObject data.  Was originally 
	/// a lighter-weight TCP socket listener, but a Unity bug cause it to be 
	/// redesigned:
	/// 
	/// See: https://issuetracker.unity3d.com/issues/debug-running-project-with-attached-debugger-causes-socket-exception-if-socket-is-in-another-thread
	///            
	/// 
	/// The server spins a new thread and listens for a HTTPRequest.  When its
	/// received it makes sure its either an alive or action endpoint.
	/// Alive will always return its alive string, and is used by appium drivers
	/// to signal that actual HCP commands are ready.  You can also use it to
	/// determine when Unity is ready.
	/// Action has a command (cmd) and parameter (params) structure.  Commands
	/// are simple strings that are matched.  On match, they spawn a Job which 
	/// has a status, JobRequest, and JobResponse.  Jobs are added to the server
	/// job queue so that they can execute on the Main thread.  Jobs may take
	/// several frames to complete.  The server waits for its status to be
	/// in a complete state prior to responding to the HTTPRequest.
	//////////////////////////////////////////////////////////////////////////
	public partial class Server : UnityEngine.MonoBehaviour
	{
		/****************************** CONSTANTS *******************************/

		/***************************** SUB-CLASSES ******************************/
		enum EServerState
		{
			STOPPED,
			STARTED
		};

		enum EServerStateTransition
		{
			NONE,
			STOPPING,
			STARTING
		};

		/***************************** GLOBAL DATA ******************************/

		/**************************** GLOBAL METHODS ****************************/

		/***************************** PUBLIC DATA ******************************/

		public HttpListener Listener { get { return m_listener; } }

		public string ListenerURI { get { return m_sListenerURI; } }

		public bool AcceptConnections { get { return this.Listener != null; } }


		//////////////////////////////////////////////////////////////////////////
		/// @brief  The appium app expects iOS data to be scaled based on 
		/// whether or not it is a retina screen.  This is really silly but
		/// it is easier to deal with here than in their codebase.  So
		/// position data could be scaled when on an iOS device.  If we didn't do
		/// this then sending touch data to iOS would require two sets of coordinate
		/// systems to be maintained: HCP-Mode and Apium-Mode.  We instead choose
		/// to emulate Appium as much as possible
		//////////////////////////////////////////////////////////////////////////
		public static float DeviceScreenScalar
		{
			get {
				// ST: Removing this retina scaling nonsense as dont see why this is needed
				/*if (Application.platform == RuntimePlatform.IPhonePlayer)
				{
					// This is taken from the AppiumInspectorScreenshotView.m file of Appium-Dot-App
					// check for retina devices
					if (Screen.width == 640 && Screen.height == 960)
					{
						// portrait 3.5" iphone with retina display
						return 2.0f;
					}
					else if (Screen.width == 960 && Screen.height == 640)
					{
						// landscape 3.5" iphone with retina display
						return 2.0f;
					}
					else if (Screen.width == 640 && Screen.height == 1136)
					{
						// portrait 4" iphone with retina display
						return 2.0f;
					}
					else if (Screen.width == 1136 && Screen.height == 640)
					{
						// landscape 4" iphone with retina display
						return 2.0f;
					}
					else if (Screen.width == 750 && Screen.height == 1334)
					{
						// portrait iphone 6
						return 2.0f;
					}
					else if (Screen.width == 1334 && Screen.height == 750)
					{
						// landscape iphone 6
						return 2.0f;
					}
					else if (Screen.width == 1242 && Screen.height == 2208)
					{
						// portrait iphone 6 plus
						return 3.0f;
					}
					else if (Screen.width == 2208 && Screen.height == 1242)
					{
						// landscape iphone 6 plus
						return 3.0f;
					}
					else if (Screen.width == 1536 && Screen.height == 2048)
					{
						// portrait ipad with retina display
						return 2.0f;
					}
					else if (Screen.width == 2048 && Screen.height == 1536)
					{
						// landscape ipad with retina display
						return 2.0f;
					}
				}*/
				return 1;
			}
		}

		/***************************** PRIVATE DATA *****************************/

		// Private but exposed via public properties above
		[UnityEngine.SerializeField] private string m_sListenerURI = "http://*:14813";

		private HttpListener m_listener = null;
		private EServerState m_state;
		private EServerStateTransition m_stateTransition;

		private event ListenerStartedEventHandler Started;
		private event LIstenerStoppedEventHandler Stopped;

		private Thread m_listenerThread;
		protected Dictionary<string, Type> m_requestCommands;
		protected Queue<Job> m_requestJobs;

		/***************************** PROPERTIES *******************************/

		/***************************** PUBLIC METHODS ***************************/

		#region API



		#endregion

		/**************************** PRIVATE METHODS ***************************/

#if USEHUTCHAPPIUM

		//////////////////////////////////////////////////////////////////////////
		/// @brief  Wait for client connection.
		//////////////////////////////////////////////////////////////////////////
		private void WaitContext()
		{
			// Start to listen for connections from a client.
			Debug.Log("Server: Waiting for a connection...");

			// Thread signal.
			const bool signaled = false;
			ManualResetEvent httpClientConnected = new ManualResetEvent(signaled);

			// Begin acceptance of requests
			this.Listener.BeginGetContext(
				(result) => 
				{
					// Retrieve the context
					try
					{
						if(this.Listener != null)
						{
							// If we are not listening this line throws a ObjectDisposedException.
							var context = this.Listener.EndGetContext(result);
					
							// Accept this context
							this.AcceptContext(context);

							Debug.Log("Server: Request processed asyncronously");
						}
					}
					catch (ObjectDisposedException)
					{
						// Intentionally not doing anything with the exception.

						Debug.Log("Server: Request no long listened to");
					}
					finally
					{
						// Signal the calling thread to continue.
						httpClientConnected.Set();
					}

				}, null );


			httpClientConnected.WaitOne();
		}
	
		//////////////////////////////////////////////////////////////////////////
		/// @brief  Process the client connection.
		//////////////////////////////////////////////////////////////////////////
		private void AcceptContext(HttpListenerContext context)
		{
			// Obtain the request.
			HttpListenerRequest request = context.Request;

			// Obtain a response object.
			HttpListenerResponse response = context.Response;
			string responseString = new Responses.ErrorResponse ().ToJSON (0).ToString();

			Debug.Log("Server: Request received: "+request.RawUrl);

			if (request.RawUrl.StartsWith ("/alive"))
			{
				// Construct a response.
				responseString = "Appium-HCP Socket Server Ready";
			}
			else if (request.RawUrl.StartsWith ("/action"))
				// https://github.com/SeleniumHQ/selenium/wiki/JsonWireProtocol
			{
				string text;
				using (var reader = new StreamReader (request.InputStream,
										request.ContentEncoding))
				{
					text = reader.ReadToEnd ();
				}

				var job = this.QueueActionRequest (text);
				job.Await ();
				responseString = job.Response.ToJSON (0).ToString();
			}

			Debug.Log("Server: constructing response...");

			// ST: log entire response for debugging in 1000 char lines (logcat maximum)
			//for(int i=0; i<responseString.Length; i+=1000)
			//	Debug.Log( ((i+1000)<responseString.Length) ? responseString.Substring(i, 1000) : responseString.Substring(i) );

			// Construct a response.
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes (responseString);

			// Get a response stream and write the response to it.
			System.IO.Stream output = response.OutputStream;
#if SEND_RESPONSE_CHUNKED
			response.SendChunked = true;
			for(int b=0; b<buffer.Length; b+=1000)
			{
				output.Write(buffer, b, Math.Min(1000, buffer.Length-b) );
				output.Flush();
			}
#else
			response.ContentLength64 = buffer.Length;
			output.Write (buffer, 0, buffer.Length);
#endif // SEND_RESPONSE_CHUNKED

			// You must close the output stream.
			output.Close ();

			Debug.Log("Server: response sent "+buffer.Length+" bytes.");
		}

		private void Transition(EServerStateTransition transition)
		{
			if(this.m_stateTransition == transition) return;
			else
			{
				if(
					transition == EServerStateTransition.STARTING && 
					m_state == EServerState.STOPPED)
				{
					m_stateTransition = EServerStateTransition.STARTING;
					this.StartServer();
					m_state = EServerState.STARTED;
					m_stateTransition = EServerStateTransition.NONE;
				}
				else if(
					transition == EServerStateTransition.STOPPING && 
					m_state == EServerState.STARTED)
				{
					m_stateTransition = EServerStateTransition.STOPPING;
					this.StopServer();
					m_state = EServerState.STOPPED;	
					m_stateTransition = EServerStateTransition.NONE;
				}
			}
		}

		private void StartServer()
		{
			Debug.Log("Server: Starting HCP Server");

			if (!HttpListener.IsSupported)
			{
				throw new InvalidOperationException("Server: The HttpListener class is unsupported!  I will not be able to provide Appium with data.");
			}

			// Prepare request listener
			m_listener = new HttpListener ();
			m_listener.Prefixes.Add (this.ListenerURI + "/alive");
			m_listener.Prefixes.Add (this.ListenerURI + "/action");
			m_listener.Prefixes.Add (this.ListenerURI + "/alive/");
			m_listener.Prefixes.Add (this.ListenerURI + "/action/");
			//m_listener.IgnoreWriteExceptions = true;						// Stops "System.Net.Sockets.SocketException: The socket has been shut down"
			//m_listener.TimeoutManager.MinSendBytesPerSecond = MAXULONG;	// .net 4.5 only so not in Unity :(
			m_listener.Start();
		
			m_listenerThread = new Thread (() => Run ());
			m_listenerThread.Start ();

			if (this.Started != null)
				this.Started ();

			Debug.Log("Server: Started HCP Server");
		}

		private void StopServer()
		{
			Debug.Log("Server: Stopping HCP Server");

			// Stop any requests
			if(this.Listener != null)
			{
				this.Listener.Stop();
				this.Listener.Close();
				this.m_listener = null;
			}

			// Stop the thread
			if (m_listenerThread != null)
			{
				m_listenerThread.Join();		// Block until thread has terninated
				m_listenerThread = null;
			}

			// Tell others that we have stopped
			if (this.Stopped != null)
				this.Stopped ();
				
			Debug.Log("Server: Stopped HCP Server");
		}

		private void CloseServer()
		{
			Debug.Log("Server: Closing HCP Server");

			// Nothing at the moment
		}

		// The main thread loop
		private void Run()
		{
			try
			{
				while(this.AcceptConnections)
				{
					this.WaitContext();
				}
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
			finally
			{
				this.Transition(EServerStateTransition.STOPPING);
			}
		}

#region Utility

		private void AddActionHandler(string requestCommand, Type requestType)
		{
			if (requestType.IsSubclassOf (typeof(JobRequest)))
			{
				this.m_requestCommands.Add (requestCommand, requestType);
			}
			else
			{
				throw new ArgumentException ("Server: requestType must be of type JobRequest");
			}
		}

		//////////////////////////////////////////////////////////////////////////
		/// @brief 	The interface to queue a job to run on the main thread
		//////////////////////////////////////////////////////////////////////////
		private Job QueueActionRequest (string task)
		{
			Job job = new Job ();

			var data = JSON.Parse (task);
			string command = data ["cmd"].Value;
			if (command == "action")
			{
				string actionCommand = data ["action"].Value;
				JSONNode parameters = data ["params"];

				if(m_requestCommands.ContainsKey(actionCommand))
				{
					Debug.Log("Server: queuing action: "+actionCommand);
					var actionType = m_requestCommands [actionCommand];
					job.Request = (JobRequest)Activator.CreateInstance (actionType, parameters);
				}
				else
				{
					Debug.Log("Server: unhandled action: "+actionCommand);
				}
			}
			else
			{
				throw new ArgumentException ("Server: Cannot queue an action of unknown command type: " + command);
			}

			m_requestJobs.Enqueue (job);
			return job;
		}

#endregion


#region Monobehavior Lifecycle

		//////////////////////////////////////////////////////////////////////////
		/// @brief Initialise class after construction.
		//////////////////////////////////////////////////////////////////////////
		private void Awake()
		{
			HutchCore.Log("Server started. Listening on URI: "+m_sListenerURI);

			// Create request builders
			this.m_requestCommands = new Dictionary<string, Type> ();
			this.AddActionHandler ("element:clearText", typeof(Requests.ClearElementTextRequest));
			this.AddActionHandler ("element:click", typeof(Requests.ClickElementRequest));
			this.AddActionHandler ("click", typeof(Requests.ComplexTapRequest)); 
			this.AddActionHandler ("find", typeof(Requests.FindElementRequest));
			this.AddActionHandler ("element:getAttribute", typeof(Requests.GetElementAttributeRequest));
			this.AddActionHandler ("element:getLocation", typeof(Requests.GetElementLocationRequest));
			this.AddActionHandler ("element:getSize", typeof(Requests.GetElementSizeRequest));
			this.AddActionHandler ("element:getText", typeof(Requests.GetElementTextRequest));
			this.AddActionHandler ("source", typeof(Requests.PageSourceRequest)); 
			this.AddActionHandler ("element:setText", typeof(Requests.SetElementTextRequest));
			this.AddActionHandler ("element:reflect", typeof(Requests.ElementReflectionRequest));

			// I don't think these touch handlers are needed.
			this.AddActionHandler ("element:touchDown", typeof(Requests.TouchDownElementRequest));
			this.AddActionHandler ("element:touchLongClick", typeof(Requests.TouchLongClickElementRequest));
			this.AddActionHandler ("element:touchMove", typeof(Requests.TouchMoveElementRequest));
			this.AddActionHandler ("element:touchUp", typeof(Requests.TouchUpElementRequest));

			// Prepare jobs queue
			m_requestJobs = new Queue<Job> ();
		}

		//////////////////////////////////////////////////////////////////////////
		/// @brief	Everything is awake, script is about to start running.
		//////////////////////////////////////////////////////////////////////////
		private void Start()
		{
		}

		//////////////////////////////////////////////////////////////////////////
		/// @brief	
		//////////////////////////////////////////////////////////////////////////
		private void OnEnable()
		{		
			this.Transition(EServerStateTransition.STARTING);
		}

		//////////////////////////////////////////////////////////////////////////
		/// @brief	
		//////////////////////////////////////////////////////////////////////////
		private void OnDisable()
		{
			this.Transition(EServerStateTransition.STOPPING);
		}

		//////////////////////////////////////////////////////////////////////////
		/// @brief 	Called when destroyed.
		//////////////////////////////////////////////////////////////////////////
		private void OnDestroy()
		{
			this.CloseServer ();
		}

		//////////////////////////////////////////////////////////////////////////
		/// @brief 	Update one time step.
		//////////////////////////////////////////////////////////////////////////
		private void Update()
		{
			if (m_requestJobs.Count > 0)
			{
				var job = m_requestJobs.Peek ();

				try
				{ 
					job.Process ();
				}
				catch (Exception e)
				{
					job.State = Job.EState.ERROR;
					Debug.LogException(e);
				}
				finally
				{
					if (job.IsComplete)
					{
						m_requestJobs.Dequeue ();
						job.Dispose ();
					}
				}
			}
		}

		#endregion
#endif // USEHUTCHAPPIUM
	}
}
