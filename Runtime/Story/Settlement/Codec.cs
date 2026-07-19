using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using Newtonsoft.Json;

namespace GameDeveloperKit.Story.Settlement
{
    public static class SettlementPlanCodec
    {
        public static string Serialize(SettlementPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var data = new PlanData
            {
                SettlementId = plan.SettlementId,
                Version = plan.Version
            };
            for (var i = 0; i < plan.Operations.Count; i++)
            {
                data.Operations.Add(ToData(plan.Operations[i]));
            }

            return JsonConvert.SerializeObject(data);
        }

        public static bool TryDeserialize(string json, out SettlementPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Settlement plan cannot be empty.";
                return false;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<PlanData>(json);
                if (data == null || data.Version != SettlementPlan.CurrentVersion || data.Operations == null)
                {
                    error = "Settlement plan version is invalid or unsupported.";
                    return false;
                }

                var operations = new List<SettlementOperation>(data.Operations.Count);
                for (var i = 0; i < data.Operations.Count; i++)
                {
                    operations.Add(FromData(data.Operations[i], i));
                }

                plan = new SettlementPlan(data.SettlementId, data.Version, operations);
                return true;
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static OperationData ToData(SettlementOperation operation)
        {
            var data = new OperationData
            {
                OperationId = operation.OperationId,
                Kind = operation.Kind
            };
            foreach (var pair in operation.Arguments.Values)
            {
                data.Arguments.Add(pair.Key, ToData(pair.Value));
            }

            return data;
        }

        private static SettlementOperation FromData(OperationData data, int index)
        {
            if (data == null)
            {
                throw new ArgumentException($"Settlement operation cannot be null. index:{index}");
            }

            var values = new Dictionary<string, Value>(StringComparer.Ordinal);
            if (data.Arguments != null)
            {
                foreach (var pair in data.Arguments)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || values.ContainsKey(pair.Key))
                    {
                        throw new ArgumentException($"Settlement argument key is invalid or duplicated. operation:{data.OperationId}");
                    }

                    values.Add(pair.Key, FromData(pair.Value, data.OperationId, pair.Key));
                }
            }

            return new SettlementOperation(data.OperationId, data.Kind, new ArgumentBag(values));
        }

        private static ValueData ToData(Value value)
        {
            switch (value.Kind)
            {
                case ValueKind.Null:
                    return new ValueData { Kind = "null" };
                case ValueKind.Boolean:
                    return new ValueData { Kind = "boolean", BooleanValue = value.BooleanValue };
                case ValueKind.Number:
                    if (double.IsNaN(value.NumberValue) || double.IsInfinity(value.NumberValue))
                    {
                        throw new ArgumentException("Settlement number argument must be finite.");
                    }

                    return new ValueData { Kind = "number", NumberValue = value.NumberValue };
                case ValueKind.String:
                    return new ValueData { Kind = "string", StringValue = value.StringValue ?? string.Empty };
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static Value FromData(ValueData data, string operationId, string key)
        {
            if (data == null)
            {
                throw new ArgumentException($"Settlement argument value cannot be null. operation:{operationId} argument:{key}");
            }

            switch (data.Kind)
            {
                case "null":
                    return Value.Null;
                case "boolean":
                    return Value.FromBoolean(data.BooleanValue);
                case "number":
                    if (double.IsNaN(data.NumberValue) || double.IsInfinity(data.NumberValue))
                    {
                        break;
                    }

                    return Value.FromNumber(data.NumberValue);
                case "string":
                    return Value.FromString(data.StringValue ?? string.Empty);
            }

            throw new ArgumentException($"Settlement argument value is invalid. operation:{operationId} argument:{key}");
        }

        [Serializable]
        private sealed class PlanData
        {
            [JsonProperty("settlementId", Order = 0)] public string SettlementId { get; set; }
            [JsonProperty("version", Order = 1)] public int Version { get; set; }
            [JsonProperty("operations", Order = 2)] public List<OperationData> Operations { get; set; } = new List<OperationData>();
        }

        [Serializable]
        private sealed class OperationData
        {
            [JsonProperty("operationId", Order = 0)] public string OperationId { get; set; }
            [JsonProperty("kind", Order = 1)] public string Kind { get; set; }
            [JsonProperty("arguments", Order = 2)] public Dictionary<string, ValueData> Arguments { get; set; } = new Dictionary<string, ValueData>(StringComparer.Ordinal);
        }

        [Serializable]
        private sealed class ValueData
        {
            [JsonProperty("kind", Order = 0)] public string Kind { get; set; }
            [JsonProperty("boolean", Order = 1)] public bool BooleanValue { get; set; }
            [JsonProperty("number", Order = 2)] public double NumberValue { get; set; }
            [JsonProperty("string", Order = 3)] public string StringValue { get; set; }
        }
    }
}
