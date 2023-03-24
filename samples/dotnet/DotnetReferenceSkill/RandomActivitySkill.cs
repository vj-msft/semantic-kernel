// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace DotnetReferenceSkill;

public class RandomActivitySkill
{
    [SKFunction("Gets a random activity from an API")]
    [SKFunctionName("GetRandomActivity")]
    public async Task<string> GetRandomActivityAsync(string input, SKContext context)
    {
        using (var httpClient = new HttpClient())
        {
            var result = await httpClient.GetStringAsync("https://www.boredapi.com/api/activity");
            var activity = JsonSerializer.Deserialize<Activity>(result);

            return activity.activity;
        }
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
