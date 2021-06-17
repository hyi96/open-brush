﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using SimpleJSON;
using TiltBrush;
using UnityEngine;
using UnityEngine.Networking;
using Object = System.Object;


public class ApiManager : MonoBehaviour
{
    private const string ROOT_API_URL = "/api/v1";
    private const string BASE_USER_SCRIPTS_URL = "/scripts";
    private const string BASE_EXAMPLE_SCRIPTS_URL = "/examplescripts";
    private const string BASE_HTML = @"<!doctype html><html lang='en'>
<head><meta charset='UTF-8'></head>
<body>{0}</body></html>";
    
    
    private FileWatcher m_FileWatcher;
    private string m_UserScriptsPath;
    private Queue m_RequestedCommandQueue = Queue.Synchronized(new Queue());
    private static ApiManager m_Instance;
    private Dictionary<string, ApiEndpoint> endpoints;

    [NonSerialized] public Vector3 BrushOrigin = new Vector3(0, 13, 3);
    [NonSerialized] public Quaternion BrushInitialRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    [NonSerialized] public Vector3 BrushPosition = new Vector3(0, 13, 3);  // Good origin for monoscopic
    [NonSerialized] public Quaternion BrushRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    private Dictionary<string, string> m_UserScripts;
    private Dictionary<string, string> m_ExampleScripts;

    public static ApiManager Instance
    {
        get { return m_Instance; }
    }
    [NonSerialized]public Stack<(Vector3, Quaternion)> BrushTransformStack;
    public string UserScriptsPath() { return m_UserScriptsPath; }

    void Awake()
    {
        m_Instance = this;
        m_UserScriptsPath = Path.Combine(App.UserPath(), "Scripts");
        App.HttpServer.AddHttpHandler($"/help", InfoCallback);
        App.HttpServer.AddHttpHandler($"/help/commands", InfoCallback);
        App.HttpServer.AddHttpHandler($"/help/brushes", InfoCallback);
        PopulateApi();
        m_UserScripts = new Dictionary<string, string>();
        m_ExampleScripts = new Dictionary<string, string>();
        PopulateExampleScripts();
        PopulateUserScripts();
        BrushTransformStack = new Stack<(Vector3, Quaternion)>();
        if (!Directory.Exists(m_UserScriptsPath))
        {
            Directory.CreateDirectory(m_UserScriptsPath);
        }
        if (Directory.Exists(m_UserScriptsPath))
        {
            m_FileWatcher = new FileWatcher(m_UserScriptsPath, "*.html");
            m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            m_FileWatcher.FileChanged += OnScriptsDirectoryChanged;
            m_FileWatcher.FileCreated += OnScriptsDirectoryChanged;
            // m_FileWatcher.FileDeleted += OnScriptsDirectoryChanged; TODO
            m_FileWatcher.EnableRaisingEvents = true;
        }
    }
    private void OnScriptsDirectoryChanged(object sender, FileSystemEventArgs e)
    {
        var fileinfo = new FileInfo(e.FullPath);
        RegisterUserScript(fileinfo);
    }

    private string InfoCallback(HttpListenerRequest request)
    {
        string html;
        StringBuilder builder;
        switch (request.Url.Segments.Last())
        {
            case "commands":
                
                if (request.Url.Query.Contains("raw"))
                {
                    html = String.Join("\n", endpoints.Keys);
                }
                else if (request.Url.Query.Contains("json"))
                {
                    html = JsonConvert.SerializeObject(ListApiCommands(), Formatting.Indented);
                }
                else
                {
                    var commandList = ListApiCommandsAsStrings();
                    builder = new StringBuilder("<h3>Open Brush API Commands</h3>");
                    builder.AppendLine("<p>To run commands a request to this url with http://localhost:40074/api/v1?</p>");
                    builder.AppendLine("<p>Commands are querystring parameters: commandname=parameters</p>");
                    builder.AppendLine("<p>Separate multiple commands with &</p>");
                    builder.AppendLine("<p>Example: <a href='http://localhost:40074/api/v1?brush.turn.y=45&brush.draw=1'>http://localhost:40074/api/v1?brush.turn.y=45&brush.draw=1</a></p>");
                    builder.AppendLine("<dl>");
                    foreach (var key in commandList.Keys)
                    {
                        string paramList = commandList[key].Item1;
                        if (paramList != "")
                        {
                            paramList = $"({paramList})";
                        }
                        builder.AppendLine($"<dt>{key} {paramList}</dt><dd>{commandList[key].Item2}</dd>");
                    }
                    builder.AppendLine("</dl>");
                    html = String.Format(BASE_HTML, builder);
                }
                break;
            case "brushes":
                var brushes = BrushCatalog.m_Instance.AllBrushes.Where(x=>x.DurableName!="");
                if (request.Url.Query.Contains("raw"))
                {
                    html = String.Join("\n", brushes.Select(x=>x.DurableName));
                }
                else
                {
                    builder = new StringBuilder("<h3>Open Brush Brushes</h3>");
                    builder.AppendLine("<ul>");
                    foreach (var b in brushes)
                    {
                        builder.AppendLine($"<li>{b.DurableName}</li>");
                    }
                    builder.AppendLine("</ul>");
                    html = String.Format(BASE_HTML, builder);
                }
                break;
            case "help":
            default:
                html = $@"<h3>Open Brush API Help</h3>
<ul>
<li>List of API commands: <a href='/help/commands'>/help/commands</a></li>
<li>List of brushes: <a href='/help/brushes'>/help/brushes</a></li>
<li>User Scripts: <a href='{BASE_USER_SCRIPTS_URL}'>{BASE_USER_SCRIPTS_URL}</a></li>
<li>Example Scripts: <a href='{BASE_EXAMPLE_SCRIPTS_URL}'>{BASE_EXAMPLE_SCRIPTS_URL}</a></li>
</ul>";
                break;
        }
        return html;
    }

    private void PopulateExampleScripts()
    {
        App.HttpServer.AddHttpHandler(BASE_EXAMPLE_SCRIPTS_URL, ExampleScriptsCallback);
        var exampleScripts = Resources.LoadAll("ScriptExamples", typeof(TextAsset));
        foreach (TextAsset htmlFile in exampleScripts)
        {
            string filename = $"{BASE_EXAMPLE_SCRIPTS_URL}/{htmlFile.name}.html";
            m_ExampleScripts[filename] = htmlFile.ToString();
            App.HttpServer.AddHttpHandler(filename, ExampleScriptsCallback);
        }
    }
    
    private void PopulateUserScripts()
    {
        App.HttpServer.AddHttpHandler(BASE_USER_SCRIPTS_URL, UserScriptsCallback);
        if (!Directory.Exists(m_UserScriptsPath))
        {
            Directory.CreateDirectory(m_UserScriptsPath);
        }
        if (Directory.Exists(m_UserScriptsPath))
        {
            var dirInfo = new DirectoryInfo(m_UserScriptsPath);
            FileInfo[] AllFileInfo = dirInfo.GetFiles();
            foreach (FileInfo fileinfo in AllFileInfo)
            {
                RegisterUserScript(fileinfo);
            }
        }
    }
    private void RegisterUserScript(FileInfo file)
    {
        if (file.Extension==".html" || file.Extension==".htm")
        {
            var f = file.OpenText();
            string filename = $"{BASE_USER_SCRIPTS_URL}/{file.Name}";
            m_UserScripts[filename] = f.ReadToEnd();
            f.Close();
            if (!App.HttpServer.HttpHandlerExists(filename))
            {
                App.HttpServer.AddHttpHandler(filename, UserScriptsCallback);
            }
        }
    }

    private void PopulateApi()
    {
        endpoints = new Dictionary<string, ApiEndpoint>();
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(t => t.GetTypes())
            .Where(t => t.IsClass && t.Namespace == "TiltBrush");

        foreach (var type in types)
        {
            foreach (MethodInfo methodInfo in type.GetMethods())
            {
                var attrs = Attribute.GetCustomAttributes(methodInfo, typeof(ApiEndpoint));
                foreach (Attribute attr in attrs)
                {
                    ApiEndpoint apiEndpoint = (ApiEndpoint)attr;
                    bool valid = false;
                    if (type.IsAbstract && type.IsSealed) // therefore is static
                    {
                        apiEndpoint.instance = null;
                        valid = true;
                    }
                    else if (type.IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        apiEndpoint.instance = FindObjectOfType(type);
                        if (apiEndpoint.instance != null)
                        {
                            valid = true;
                        }
                        else
                        {
                            Debug.LogWarning($"No instance found for ApiEndpoint on: {type}");
                        }
                    }
                    
                    if (valid)
                    {
                        apiEndpoint.type = type;
                        apiEndpoint.methodInfo = methodInfo;
                        apiEndpoint.parameterInfo = methodInfo.GetParameters();
                        endpoints[apiEndpoint.Endpoint] = apiEndpoint;
                    }
                    else
                    {
                        Debug.LogWarning($"ApiEndpoint declared on invalid class: {type}");
                    }
                }
            }
        }
        App.HttpServer.AddHttpHandler(ROOT_API_URL, ApiCommandCallback);
    }
    
    public bool InvokeEndpoint(KeyValuePair<string, string> command)
    {
        if (endpoints.ContainsKey(command.Key))
        {
            var endpoint = endpoints[command.Key];
            var parameters = endpoint.DecodeParams(command.Value);
            endpoint.Invoke(parameters);
            return true;
        }
        else
        {
            Debug.LogError($"Invalid API command: {command.Key}");
        }
        return false;
    }
    [ContextMenu("Log Api Commands")]
    public void LogCommandsList()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Please run in play mode");
        }
        else
        {
            var builder = new StringBuilder();
            var commands = ListApiCommandsAsStrings();
            foreach (var k in commands.Keys)
            {
                builder.AppendLine($"{k} ({commands[k].Item2}): {commands[k].Item2}");
            }
        }
    }
    
    Dictionary<string, (string, string)> ListApiCommandsAsStrings()
    {
        var commandList = new Dictionary<string, (string, string)>();
        foreach (var endpoint in endpoints.Keys)
        {
            var paramInfoText = new List<string>();
            foreach (var param in endpoints[endpoint].parameterInfo)
            {
                string typeName = param.ParameterType.Name
                    .Replace("Single", "float")
                    .Replace("Int32", "int")
                    .Replace("String", "string");
                paramInfoText.Add($"{typeName} {param.Name}");
            }
            string paramInfo = String.Join(", ", paramInfoText);
            commandList[endpoint] = (paramInfo, endpoints[endpoint].Description);
        }
        return commandList;
    }
    
    Dictionary<string, object> ListApiCommands()
    {
        var commandList = new Dictionary<string, object>();
        foreach (var endpoint in endpoints.Keys)
        {
            commandList[endpoint] = new
            {
                parameters = endpoints[endpoint].ParamsAsDict(),
                description = endpoints[endpoint].Description
            };
        }
        return commandList;
    }

    private string UserScriptsCallback(HttpListenerRequest request)
    {
        string html;
        if (request.Url.Segments.Length == 2)
        {
            var builder = new StringBuilder("<h3>Open Brush User Scripts</h3>");
            builder.AppendLine("<ul>");
            foreach (var e in m_UserScripts)
            {
                builder.AppendLine($"<li><a href='{e.Key}'>{e.Key}</a></li>");
            }
                            
            // Only show this button on Windows
            // TODO Update this is ApiMethods.OpenUserFolder is ever cross platform
            // (Also see similar global commands that will need updating)
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                builder.AppendLine($"<button onclick=\"fetch('{ROOT_API_URL}?showfolder.scripts');\">Open Scripts Folder</button>");
            }
            builder.AppendLine("</ul>");
            html = String.Format(BASE_HTML, builder);
        }
        else
        {
            html = m_UserScripts[Uri.UnescapeDataString(request.Url.AbsolutePath)];
        }
        return ScriptTemplateSubstitution(html);
    }

    private string ExampleScriptsCallback(HttpListenerRequest request)
    {
        string html;
        if (request.Url.Segments.Length == 2)
        {
            var builder = new StringBuilder("<h3>Open Brush Example Scripts</h3>");
            builder.AppendLine("<ul>");
            foreach (var e in m_ExampleScripts)
            {
                builder.AppendLine($"<li><a href='{e.Key}'>{e.Key}</a></li>");
            }
            builder.AppendLine("</ul>");
            html = String.Format(BASE_HTML, builder);
        }
        else
        {
            html = m_ExampleScripts[Uri.UnescapeDataString(request.Url.AbsolutePath)];
        }
        return ScriptTemplateSubstitution(html);
    }
    
    private string ScriptTemplateSubstitution(string html)
    {
        string[] brushNameList = BrushCatalog.m_Instance.AllBrushes.Where(x => x.DurableName != "").Select(x => x.DurableName).ToArray();
        string brushesJson = JsonConvert.SerializeObject(brushNameList);
        string commandsJson = JsonConvert.SerializeObject(ListApiCommands());
        html = html.Replace("{{brushesJson}}", brushesJson);
        html = html.Replace("{{commandsJson}}", commandsJson);
        return html;
    }

    string ApiCommandCallback(HttpListenerRequest request)
    {

        KeyValuePair<string, string> command;

        // Handle GET
        foreach (string pair in request.Url.Query.TrimStart('?').Split('&'))
        {
            string[] kv = pair.Split(new[]{'='}, 2);
            if (kv.Length == 1 && kv[0]!="")
            {
                m_RequestedCommandQueue.Enqueue(new KeyValuePair<string, string>(kv[0], ""));
            }
            else if (kv.Length == 2)
            {
                m_RequestedCommandQueue.Enqueue(new KeyValuePair<string, string>(kv[0], UnityWebRequest.UnEscapeURL(kv[1])));
            }
        }
        
        // Handle POST
        // TODO also accept JSON
        if (request.HasEntityBody)
        {
            using (Stream body = request.InputStream)
            {
                using (var reader = new StreamReader(body, request.ContentEncoding))
                {
                    var formdata = Uri.UnescapeDataString(reader.ReadToEnd());
                    var pairs = formdata.Replace("+", " ").Split('&');
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split(new[]{'='}, 2);
                        command = new KeyValuePair<string, string>(kv[0], kv[1]);
                        m_RequestedCommandQueue.Enqueue(command);
                    }
                }
            }
        }
        
        return "OK";
    }

    private bool HandleApiCommand()
    {
        KeyValuePair<string, string> command;
        try
        {
            command = (KeyValuePair<string, string>)m_RequestedCommandQueue.Dequeue();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        return Instance.InvokeEndpoint(command);
    }

    private void Update()
    {
        HandleApiCommand();
    }

}
