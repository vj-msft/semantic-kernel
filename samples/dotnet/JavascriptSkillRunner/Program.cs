// Copyright (c) Microsoft. All rights reserved.

using DotnetReferenceSkill;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;

const string RANDOM_ACTIVITY_PROMPT = "Find me an activity to do, return only a single activity. I like to {{$INPUT}}. Be creative!";
const string DEGREES_OF_SEPARATION_PROMPT = "How many degrees of separation are there between {{$ITEM1}} and {{$ITEM2}}? Bonus points for Kevin Bacon reference.";

//configure your Azure OpenAI backend
var key = "";
var endpoint = "";
var model = "";


var sk = Kernel.Builder.Configure(c => c.AddAzureOpenAICompletionBackend(model, model, endpoint, key)).Build();

#region Semantic Function Loading
sk.CreateSemanticFunction(RANDOM_ACTIVITY_PROMPT,
            skillName: "RandomActivityLLMSkill",
            functionName: "GetRandomActivity",
            description: "Finds a random activity based on the interest input",
            maxTokens: 256,
            temperature: 0.1,
            topP: 0.5);

sk.CreateSemanticFunction(DEGREES_OF_SEPARATION_PROMPT,
            skillName: "DegreesOfSeparation",
            functionName: "FindDegrees",
            description: "Returns the degrees of separation between two concepts.",
            maxTokens: 256,
            temperature: 0.2,
            topP: 0.5);
#endregion

//TODO: load your python skill here
//currently loading a skill that will pull a random activity from an API.
//We will compare the activity to the one from the LLM and return true if they match, false if not

//create object for JavascriptSkill
var jsSkill = new JavascriptSkill();
sk.ImportSkill(jsSkill, nameof(JavascriptSkill));

/*var randomActivitySkill = new RandomActivitySkill();
sk.ImportSkill(randomActivitySkill, nameof(RandomActivitySkill));*/

var thingiliketodo = "surf";
var llmResult = string.Empty;
var apiResult = string.Empty;

//ask the LLM for a random activity
var llmRandomActivityResult = await sk.RunAsync(thingiliketodo, sk.Skills.GetSemanticFunction("RandomActivityLLMSkill", "GetRandomActivity"));
llmResult = llmRandomActivityResult.Result;
Console.WriteLine("LLM suggested: " + llmResult);

//ask the skill to find us a random activity via API
var skillApiRandomActivityResult = await sk.RunAsync(sk.Skills.GetNativeFunction(nameof(JavascriptSkill), "GetjsSkill"));
apiResult = skillApiRandomActivityResult.Result;
Console.WriteLine("Skill suggested: " + apiResult);

//lastly, for fun, find out how many degrees of separation exist between these concepts :)
var contextVariables = new ContextVariables();
contextVariables.Set("Item1", llmResult);
contextVariables.Set("Item2", apiResult);

var llmDegreesOfSeparationResult = await sk.RunAsync(contextVariables, sk.Skills.GetSemanticFunction("DegreesOfSeparation", "FindDegrees"));
Console.WriteLine("Degrees of separation: " + llmDegreesOfSeparationResult.Result);
