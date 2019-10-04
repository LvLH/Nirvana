﻿using System;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;
using IO;
using Newtonsoft.Json;

namespace Cloud.Utilities
{
    public static class LogUtilities
    {
        public static void LogLambdaInfo(ILambdaContext context, string version) => Logger.LogLine(
            $"Lambda version: {version} ARN: {context?.InvokedFunctionArn}\nLog group: {context?.LogGroupName}\nLog stream: {context?.LogStreamName}");

        public static void LogObject<T>(string title, T config)
        {
            switch (config)
            {
                case string s:
                    Logger.LogLine($"{title}:\n{s}");
                    break;
                default:
                    Logger.LogLine($"{title}:\n{JsonConvert.SerializeObject(config)}");
                    break;
            }
        }

        public static void Log(IEnumerable<string> environmentVariables)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Environment variables:");

            foreach (string key in environmentVariables)
            {
                string value = Environment.GetEnvironmentVariable(key) ?? "null";
                sb.AppendLine($"- {key}: {value}");
            }

            Logger.LogLine(sb.ToString());
        }

        public static void UpdateLogger(ILambdaLogger logger, StringBuilder sb)
        {
            Logger.LogLine = s =>
            {
                logger.LogLine(s);
                sb?.Append(s + "\n");
            };
        }
    }
}
