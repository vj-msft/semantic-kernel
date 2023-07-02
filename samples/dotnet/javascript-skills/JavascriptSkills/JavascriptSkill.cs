// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Dynamic;
using System.Net.Http;
using System.Text.Json;
using Jint;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace JavascriptSkills;
public class JavascriptSkill
{
    [SKFunction("Gets a random activity from an API")]
    [SKFunctionName("GetjsSkill")]
    public async Task<string> GetRandomActivityAsync(string input, SKContext context)
    {
        string url = "https://www.boredapi.com/api/activity";
        try
        {
            var engine = new Engine();
            var script = @"
                                let client = new http();
                                 res =  client.HttpGetAsync(url);";

            var response = engine
                 .SetValue("url", url)
                 .SetValue("http", Jint.Runtime.Interop.TypeReference.CreateTypeReference(engine, typeof(HttpPlugin)))
                 .Execute(script)
                 .GetCompletionValue().AsString();

            var activity = JsonSerializer.Deserialize<Activity>(response);
            return activity.activity;
        }
        catch (Exception exception)
        {
            var msg = exception.Message;
            return msg;
        }

    }
    public class Activity
    {
        public string activity { get; set; }
        public string type { get; set; }
        public int participants { get; set; }
        public double price { get; set; }
        public string link { get; set; }
        public string key { get; set; }
        public float accessibility { get; set; }
    }

}

