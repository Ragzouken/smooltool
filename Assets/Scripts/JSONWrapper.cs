using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class JsonWrapper
{
    public static JsonSerializerSettings Settings()
    {
        JsonSerializerSettings settings = new JsonSerializerSettings();
        settings.TypeNameHandling = TypeNameHandling.Auto;
        settings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
        settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
        settings.Converters.Add(new Vector2Converter());
        settings.Converters.Add(new ColorConverter());
        settings.Converters.Add(new ByteSetConverter());
        settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
        settings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;

        return settings;
    }

    public static T Deserialise<T>(string value)
    {
        T returnObject = JsonConvert.DeserializeObject<T>(value, Settings());

        return returnObject;
    }

    public static string Serialise<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, Formatting.Indented, Settings());
    }

    public static T Copy<T>(T obj)
    {
        return Deserialise<T>(Serialise(obj));
    }
}

public class Vector2Converter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Vector2);
    }

    public override object ReadJson(JsonReader reader, 
                                    Type objectType, 
                                    object existingValue, 
                                    JsonSerializer serializer)
    {
        Vector2 vector = new Vector2();

        vector.x = (float) reader.ReadAsDecimal().GetValueOrDefault();
        vector.y = (float) reader.ReadAsDecimal().GetValueOrDefault();
        reader.Read();

        return vector;
    }

    public override void WriteJson(JsonWriter writer, 
                                   object value, 
                                   JsonSerializer serializer)
    {
        Vector2 vector = (Vector2) value;

        writer.WriteStartArray();
        writer.WriteValue(vector.x);
        writer.WriteValue(vector.y);
        writer.WriteEndArray();
    }
}

public class ColorConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Color)
            || objectType == typeof(Color32);
    }

    public override object ReadJson(JsonReader reader,
                                    Type objectType,
                                    object existingValue,
                                    JsonSerializer serializer)
    {
        Color32 color = new Color32();

        color.r = (byte) reader.ReadAsDecimal().GetValueOrDefault();
        color.g = (byte) reader.ReadAsDecimal().GetValueOrDefault();
        color.b = (byte) reader.ReadAsDecimal().GetValueOrDefault();
        color.a = (byte) reader.ReadAsDecimal().GetValueOrDefault();
        reader.Read();

        if (objectType == typeof(Color)) return (Color) color;

        return color;
    }

    public override void WriteJson(JsonWriter writer,
                                   object value,
                                   JsonSerializer serializer)
    {
        Color32 color = default(Color32);
        
        if (value is Color)   color = (Color)   value;
        if (value is Color32) color = (Color32) value;

        writer.WriteStartArray();
        writer.WriteValue(color.r);
        writer.WriteValue(color.g);
        writer.WriteValue(color.b);
        writer.WriteValue(color.a);
        writer.WriteEndArray();
    }
}

public class ByteSetConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(HashSet<byte>);
    }

    public override object ReadJson(JsonReader reader,
                                    Type objectType,
                                    object existingValue,
                                    JsonSerializer serializer)
    {
        var members = serializer.Deserialize<byte[]>(reader);
        
        //reader.Read();

        return new HashSet<byte>(members);
    }

    public override void WriteJson(JsonWriter writer,
                                   object value,
                                   JsonSerializer serializer)
    {
        var set = (HashSet<byte>) value;

        byte[] members = set.ToArray();

        serializer.Serialize(writer, members);
    }
}
