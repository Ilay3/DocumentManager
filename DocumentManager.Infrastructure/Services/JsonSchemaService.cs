using DocumentManager.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentManager.Infrastructure.Services
{
    public class JsonSchemaService : IJsonSchemaService
    {
        private readonly string _basePath;

        public JsonSchemaService(string basePath)
        {
            _basePath = basePath;
        }

        public async Task<Dictionary<string, object>> LoadJsonSchemaAsync(string jsonPath)
        {
            var fullPath = Path.Combine(_basePath, jsonPath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"JSON schema file not found: {fullPath}");
            }

            var jsonContent = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
        }

        public IEnumerable<Dictionary<string, object>> GetFieldsFromSchema(Dictionary<string, object> schema)
        {
            if (schema.TryGetValue("fields", out var fieldsObj))
            {
                var fieldsJson = JsonSerializer.Serialize(fieldsObj);
                return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(fieldsJson);
            }

            return new List<Dictionary<string, object>>();
        }

        public bool ValidateFieldValue(Dictionary<string, object> fieldSchema, string value)
        {
            // Проверка на обязательность
            if (fieldSchema.TryGetValue("isRequired", out var isRequiredObj) && (bool)isRequiredObj)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }
            }

            // Проверка типа поля
            if (fieldSchema.TryGetValue("fieldType", out var fieldTypeObj))
            {
                var fieldType = fieldTypeObj.ToString();

                switch (fieldType)
                {
                    case "date":
                        return DateTime.TryParse(value, out _);

                    case "select":
                        if (fieldSchema.TryGetValue("options", out var optionsObj))
                        {
                            var optionsJson = JsonSerializer.Serialize(optionsObj);
                            var options = JsonSerializer.Deserialize<List<string>>(optionsJson);
                            return options.Contains(value);
                        }
                        break;
                }
            }

            return true;
        }

        public Dictionary<string, object> GetFieldCondition(Dictionary<string, object> fieldSchema)
        {
            if (fieldSchema.TryGetValue("condition", out var conditionObj))
            {
                var conditionJson = JsonSerializer.Serialize(conditionObj);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(conditionJson);
            }

            return null;
        }
    }

}
