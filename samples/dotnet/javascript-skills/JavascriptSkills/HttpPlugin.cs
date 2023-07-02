// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace JavascriptSkills;
internal class HttpPlugin
{
    public string HttpGetAsync(string url)
    {
        string result = "";
        using (var httpClient = new HttpClient())
        {
            var response = httpClient.GetStringAsync(url).Result;
            result = response;
        }
        return result;
    }
}
