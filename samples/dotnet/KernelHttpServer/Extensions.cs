// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using KernelHttpServer.Config;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Document;
using Microsoft.SemanticKernel.Skills.Document.FileSystem;
using Microsoft.SemanticKernel.Skills.Document.OpenXml;
using Microsoft.SemanticKernel.Skills.MsGraph;
using Microsoft.SemanticKernel.Skills.MsGraph.Connectors;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.TemplateEngine;
using static KernelHttpServer.Config.Constants;
using Directory = System.IO.Directory;

namespace KernelHttpServer;

internal static class Extensions
{
    internal static ApiKeyConfig ToApiKeyConfig(this HttpRequestData req)
    {
        var apiConfig = new ApiKeyConfig();

        // completion values
        if (req.Headers.TryGetValues(SKHttpHeaders.CompletionBackend, out var completionAIService))
        {
            apiConfig.CompletionConfig.AIService = Enum.Parse<AIService>(completionAIService.First());
        }

        if (req.Headers.TryGetValues(SKHttpHeaders.CompletionModel, out var completionModelValue))
        {
            apiConfig.CompletionConfig.DeploymentOrModelId = completionModelValue.First();
            apiConfig.CompletionConfig.Label = apiConfig.CompletionConfig.DeploymentOrModelId;
        }

        if (req.Headers.TryGetValues(SKHttpHeaders.CompletionEndpoint, out var completionEndpoint))
        {
            apiConfig.CompletionConfig.Endpoint = completionEndpoint.First();
        }

        if (req.Headers.TryGetValues(SKHttpHeaders.CompletionKey, out var completionKey))
        {
            apiConfig.CompletionConfig.Key = completionKey.First();
        }

        // embedding values
        if (req.Headers.TryGetValues(SKHttpHeaders.EmbeddingBackend, out var embeddingAIService))
        {
            apiConfig.EmbeddingConfig.AIService = Enum.Parse<AIService>(embeddingAIService.First());
        }

        if (req.Headers.TryGetValues(SKHttpHeaders.EmbeddingModel, out var embeddingModelValue))
        {
            apiConfig.EmbeddingConfig.DeploymentOrModelId = embeddingModelValue.First();
            apiConfig.EmbeddingConfig.Label = apiConfig.EmbeddingConfig.DeploymentOrModelId;
        }

        if (req.Headers.TryGetValues(SKHttpHeaders.EmbeddingEndpoint, out var embeddingEndpoint))
        {
            apiConfig.EmbeddingConfig.Endpoint = embeddingEndpoint.First();
        }

        if (req.Headers.TryGetValues(SKHttpHeaders.EmbeddingKey, out var embeddingKey))
        {
            apiConfig.EmbeddingConfig.Key = embeddingKey.First();
        }

        return apiConfig;
    }

    internal static async Task<HttpResponseData> CreateResponseWithMessageAsync(this HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        HttpResponseData response = req.CreateResponse(statusCode);
        await response.WriteStringAsync(message);
        return response;
    }

    internal static ISKFunction GetFunction(this IReadOnlySkillCollection skills, string skillName, string functionName)
    {
        return skills.HasNativeFunction(skillName, functionName)
            ? skills.GetNativeFunction(skillName, functionName)
            : skills.GetSemanticFunction(skillName, functionName);
    }

    internal static bool HasSemanticOrNativeFunction(this IReadOnlySkillCollection skills, string skillName, string functionName)
    {
        return skills.HasSemanticFunction(skillName, functionName) || skills.HasNativeFunction(skillName, functionName);
    }

    private static bool _ShouldLoad(string skillName, IEnumerable<string>? skillsToLoad = null)
    {
        return skillsToLoad?.Contains(skillName, StringComparer.InvariantCultureIgnoreCase) != false;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The caller invokes native skills during a request and the HttpClient instance must remain alive for those requests to be successful.")]
    internal static void RegisterNativeGraphSkills(this IKernel kernel, string graphToken, IEnumerable<string>? skillsToLoad = null)
    {
        IList<DelegatingHandler> handlers = GraphClientFactory.CreateDefaultHandlers(new TokenAuthenticationProvider(graphToken));
        GraphServiceClient graphServiceClient = new(GraphClientFactory.Create(handlers));

        if (_ShouldLoad(nameof(CloudDriveSkill), skillsToLoad))
        {
            CloudDriveSkill cloudDriveSkill = new(new OneDriveConnector(graphServiceClient));
            _ = kernel.ImportSkill(cloudDriveSkill, nameof(cloudDriveSkill));
        }

        if (_ShouldLoad(nameof(TaskListSkill), skillsToLoad))
        {
            TaskListSkill taskListSkill = new(new MicrosoftToDoConnector(graphServiceClient));
            _ = kernel.ImportSkill(taskListSkill, nameof(taskListSkill));
        }

        if (_ShouldLoad(nameof(EmailSkill), skillsToLoad))
        {
            EmailSkill emailSkill = new(new OutlookMailConnector(graphServiceClient));
            _ = kernel.ImportSkill(emailSkill, nameof(emailSkill));
        }

        if (_ShouldLoad(nameof(CalendarSkill), skillsToLoad))
        {
            CalendarSkill calendarSkill = new(new OutlookCalendarConnector(graphServiceClient));
            _ = kernel.ImportSkill(calendarSkill, nameof(calendarSkill));
        }
    }

    internal static void RegisterPlanner(this IKernel kernel)
    {
        PlannerSkill planner = new(kernel);
        _ = kernel.ImportSkill(planner, nameof(PlannerSkill));
    }

    internal static void RegisterTextMemory(this IKernel kernel)
    {
        _ = kernel.ImportSkill(new TextMemorySkill(), nameof(TextMemorySkill));
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
       Justification = "The caller invokes native skills during a request and the skill instances must remain alive for those requests to be successful.")]
    internal static void RegisterNativeSkills(this IKernel kernel, IEnumerable<string>? skillsToLoad = null)
    {
        if (_ShouldLoad(nameof(DocumentSkill), skillsToLoad))
        {
            DocumentSkill documentSkill = new(new WordDocumentConnector(), new LocalFileSystemConnector());
            _ = kernel.ImportSkill(documentSkill, nameof(DocumentSkill));
        }

        if (_ShouldLoad(nameof(ConversationSummarySkill), skillsToLoad))
        {
            ConversationSummarySkill conversationSummarySkill = new(kernel);
            _ = kernel.ImportSkill(conversationSummarySkill, nameof(ConversationSummarySkill));
        }

        if (_ShouldLoad(nameof(WebFileDownloadSkill), skillsToLoad))
        {
            WebFileDownloadSkill webFileDownloadSkill = new WebFileDownloadSkill();
            _ = kernel.ImportSkill(webFileDownloadSkill, nameof(WebFileDownloadSkill));
        }
    }

    internal static void RegisterSemanticSkills(
        this IKernel kernel,
        string skillsFolder,
        ILogger logger,
        IEnumerable<string>? skillsToLoad = null)
    {
        foreach (string skPromptPath in Directory.EnumerateFiles(skillsFolder, "*.txt", SearchOption.AllDirectories))
        {
            FileInfo fInfo = new(skPromptPath);
            DirectoryInfo? currentFolder = fInfo.Directory;

            while (currentFolder?.Parent?.FullName != skillsFolder)
            {
                currentFolder = currentFolder?.Parent;
            }

            if (_ShouldLoad(currentFolder.Name, skillsToLoad))
            {
                try
                {
                    _ = kernel.ImportSemanticSkillFromDirectory(skillsFolder, currentFolder.Name);
                }
                catch (TemplateException e)
                {
                    logger.LogWarning("Could not load skill from {0} with error: {1}", currentFolder.Name, e.Message);
                }
            }
        }
    }

    internal static async Task TempPopulateMemoryStoreAsync(this IKernel kernel)
    {
        // If db has been populated, don't bother again
        var collections = await kernel.Memory.GetCollectionsAsync();
        if (collections != null && collections.Contains("github"))
        {
            return;
        }

        var githubFiles = new Dictionary<string, string>()
        {
            ["ValidationException.cs"]
                = "ValidationException is a custom exception class that is used to throw exceptions when validating input. It contains a list of error codes that can be used to identify the type of exception that was thrown. It also contains constructors for setting the error code and message of the exception.",
            ["TextSkill.cs"]
                = "The TextSkill class provides a set of functions to manipulate strings, such as trimming whitespace from the start and end of a string, converting a string to uppercase, and converting a string to lowercase. It can be imported into the SemanticKernel using the ImportSkill method.",
            ["MemoryConfiguration.cs"]
                = "This code is a static class that provides methods for configuring a semantic memory with custom settings. It allows the user to specify a memory storage and an embeddings backend, and provides methods for setting the semantic memory with the given memory storage and embedding generator. It also includes error checking to ensure that the given parameters are valid.",
            ["PlanRunner.cs"]
                = "This code is a class that executes plans created by the PlannerSkill/ProblemSolver semantic function. It parses the plan xml, executes the first step, converts the resulting context into a new plan xml, and returns it. It also has methods to process the context variables and solution nodes in the plan xml.",
            ["IPromptTemplate.cs"]
                = "This code provides an interface for a prompt template which is used to render a prompt to a string. It contains two methods, GetParameters() which returns a list of parameters required by the template, and RenderAsync() which renders the template using the information in the context.",
            ["SemanticFunctionConfig.cs"]
                = "This code is a class that is used to configure a semantic function. It contains two properties, PromptTemplateConfig and PromptTemplate, which are used to store a PromptTemplateConfig object and an IPromptTemplate object respectively. It also contains a constructor that takes in two parameters, a PromptTemplateConfig object and an IPromptTemplate object, and assigns them to the respective properties.",
            ["NullMemory.cs"]
                = "This code provides an implementation of the ISemanticTextMemory interface that stores nothing. It provides methods for saving information, saving references, getting information, searching for information, and getting collections. It is a singleton instance, meaning only one instance of this class can exist at any given time.",
            ["VolatileMemoryStore.cs"]
                = "This code provides a VolatileMemoryStore class that implements the IMemoryStore interface. It is used to store embeddings of type TEmbedding and provides methods to get the nearest matches to a given embedding, calculate the cosine similarity between two embeddings, and sort the embeddings by score. It is also provided with a default constructor for a simple volatile memory embeddings store for embeddings with the default embedding type being float.",
            ["ParameterView.cs"]
                = "The ParameterView class is used to copy and export data from SKFunctionContextParameterAttribute and SKFunctionInputAttribute for planner and related scenarios. It contains a constructor and a public property for the parameter name, description, and default value. The name must be alphanumeric with underscores allowed.",
            ["TemplateException.cs"]
                = "This code defines a TemplateException class which is used to handle errors that occur while using the Microsoft SemanticKernel TemplateEngine. It contains an ErrorCodes enum which defines the different types of errors that can occur, such as SyntaxError, FunctionNotFound, and RuntimeError. It also contains several constructors which are used to create TemplateException objects with an error code, message, and existing exception.",
            ["Kernel.cs"]
                = "This code is a class that is used to create a Semantic Kernel. The Semantic Kernel provides a skill collection to define native and semantic functions, an orchestrator to execute a list of functions, and a prompt template rendering engine to render and execute semantic functions. It also provides methods to register semantic functions, import skills, and run pipelines of functions. Future versions will include additional features such as customizing the rendering engine, branching logic in the functions pipeline, persisting execution state, distributing pipelines over a network, RPC functions, secure environments, auto-generating pipelines, and more.",
            ["SKContext.cs"]
                = "The SKContext class provides a context for Semantic Kernel operations. It contains variables, semantic text memory, skills, a logger, and a cancellation token. It provides methods to access functions by skill and name, and to signal when an error occurs. It also provides a method to print the processed input, or the last exception message if an error occurred.",
            ["DefaultHttpRetryHandlerFactory.cs"]
                = "This code is a C# class used to create a DefaultHttpRetryHandler object. It has a constructor that takes an optional HttpRetryConfig parameter and a Create() method that takes an ILogger parameter and returns a DefaultHttpRetryHandler object. This class is used to create a handler object that can be used to make HTTP requests with retry logic.",
            ["OpenAICompletionTests.cs"]
                = "This code is a unit test for OpenAI and Azure OpenAI completion backends in the SemanticKernel library. It tests the ability of the library to connect to OpenAI and Azure OpenAI services, retrieve data from them, and parse the response. It also tests the ability of the library to handle errors and retry requests.",
            ["Getting-Started-Notebook.ipynb"]
                = "This code is a C# notebook that provides instructions for setting up and using the Semantic Kernel SDK. It provides instructions for setting up the SDK with an Open AI Key or Azure Open AI Service key, instantiating the kernel, loading and running a skill, and provides links to sample apps for learning how to use the SDK.",
            ["App.tsx"]
                = "This code is for a React application that provides a simple chat summary app. It imports components from the Fluent UI React library, and uses the useEffect and useState hooks. It contains a tabbed interface with Setup, Interact, and AI Summary tabs, and provides helpful tips in the sidebar. It also contains functionality for setting up a service configuration, interacting with a chat, and getting an AI summary.",
        };

        foreach (var entry in githubFiles)
        {
            await kernel.Memory.SaveInformationAsync(
                collection: "github",
                description: entry.Value,
                text: entry.Value,
                id: entry.Key
            );
        }
    }
}
