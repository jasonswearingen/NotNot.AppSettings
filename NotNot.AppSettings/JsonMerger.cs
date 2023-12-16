using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NotNot.AppSettings;

/// <summary>
/// helper to merge json files into a single object
/// </summary>
internal static class JsonMerger
{
   public static JsonDocumentOptions _options = new JsonDocumentOptions
   {
      AllowTrailingCommas = true,
      CommentHandling = JsonCommentHandling.Skip,
      MaxDepth = 64,
   };
   public static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
   {
      ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
      AllowTrailingCommas = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      MaxDepth = 10,
      NumberHandling = JsonNumberHandling.AllowReadingFromString,
      ReferenceHandler = ReferenceHandler.IgnoreCycles,
      UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
      UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
      WriteIndented = true,
   };

   public static Dictionary<string, JsonElement> MergeJsonFiles(Dictionary<string, SourceText> sourceTexts, List<Diagnostic> diagReport)
   {
	   var mergedObject = new Dictionary<string, JsonElement>();

	   foreach (var pair in sourceTexts)
	   {
			var fileName = pair.Key;
			var sourceText = pair.Value;

		   diagReport._Info($"obtaining settings from {fileName}");
		   
			using var jsonDoc = JsonDocument.Parse(sourceText.ToString(), _options);

		   MergeJson(mergedObject, jsonDoc.RootElement);
	   }
	   return mergedObject;
	}
	
	//[Obsolete("uses File.IO to read.  Works but frowned upon for sourcegen.  Switched to SourceText",true)]
	//public static Dictionary<string, JsonElement> MergeJsonFiles_FileIo(List<FileInfo> fileInfos, List<Diagnostic> diagReport)
	//{
	//	var mergedObject = new Dictionary<string, JsonElement>();

	//	foreach (var info in fileInfos)
	//	{
	//		diagReport._Info($"obtaining settings from {info.FullName}");

	//		using var jsonDoc = JsonDocument.Parse(info.OpenRead(), _options);

	//		MergeJson(mergedObject, jsonDoc.RootElement);
	//	}
	//	return mergedObject;
	//}


	/// <summary>
	/// merges json objects into a single object   /// 
	/// </summary>
	/// <param name="target"></param>
	/// <param name="source"></param>
	public static void MergeJson(Dictionary<string, JsonElement> target, JsonElement source)
   {
      if(source.ValueKind != JsonValueKind.Object)
      {
         throw new ArgumentException("source must be an object", nameof(source));
      }
      
      foreach (var property in source.EnumerateObject())
      {
         if (target.ContainsKey(property.Name))
         {
            // If both are objects, merge them
            if (target[property.Name].ValueKind == JsonValueKind.Object && property.Value.ValueKind == JsonValueKind.Object)
            {
               var mergedNestedObject = new Dictionary<string, JsonElement>();
               MergeJson(mergedNestedObject, target[property.Name]);
               MergeJson(mergedNestedObject, property.Value);
               target[property.Name] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(mergedNestedObject));
            }
            else
            {

               // If both are arrays, concatenate them
               if (target[property.Name].ValueKind == JsonValueKind.Array && property.Value.ValueKind == JsonValueKind.Array)
               {
                  var combinedArray = target[property.Name].EnumerateArray().Concat(property.Value.EnumerateArray()).ToArray();
                  target[property.Name] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(combinedArray));
               }
               // Handle other types (e.g., conflicting types) - this part depends on your specific merging strategy
               else
               {
                  // Example strategy: Overwrite with the new value
                  target[property.Name] = property.Value.Clone();
               }

            }

         }
         else
         {
            // No conflict, just add the new key-value pair
            target[property.Name] = property.Value.Clone();
         }
      }
   }
}