﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Terminal.Gui;

internal class KeyCodeJsonConverter : JsonConverter<KeyCode>
{
    public override KeyCode Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var key = KeyCode.Null;

            Dictionary<string, KeyCode> modifierDict =
                new (StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Shift", KeyCode.ShiftMask }, { "Ctrl", KeyCode.CtrlMask }, { "Alt", KeyCode.AltMask }
                };

            List<KeyCode> modifiers = new ();

            while (reader.Read ())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString ();
                    reader.Read ();

                    switch (propertyName.ToLowerInvariant ())
                    {
                        case "key":
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                if (Enum.TryParse (reader.GetString (), false, out key))
                                {
                                    break;
                                }

                                // The enum uses "D0..D9" for the number keys
                                if (Enum.TryParse (reader.GetString ()!.TrimStart ('D', 'd'), false, out key))
                                {
                                    break;
                                }

                                if (key == KeyCode.Null)
                                {
                                    throw new JsonException (
                                                             $"The value \"{reader.GetString ()}\" is not a valid Key."
                                                            );
                                }
                            }
                            else if (reader.TokenType == JsonTokenType.Number)
                            {
                                try
                                {
                                    key = (KeyCode)reader.GetInt32 ();
                                }
                                catch (InvalidOperationException ioe)
                                {
                                    throw new JsonException ($"Error parsing Key value: {ioe.Message}", ioe);
                                }
                                catch (FormatException ioe)
                                {
                                    throw new JsonException ($"Error parsing Key value: {ioe.Message}", ioe);
                                }
                            }

                            break;

                        case "modifiers":
                            if (reader.TokenType == JsonTokenType.StartArray)
                            {
                                while (reader.Read ())
                                {
                                    if (reader.TokenType == JsonTokenType.EndArray)
                                    {
                                        break;
                                    }

                                    string mod = reader.GetString ();

                                    try
                                    {
                                        modifiers.Add (modifierDict [mod]);
                                    }
                                    catch (KeyNotFoundException e)
                                    {
                                        throw new JsonException ($"The value \"{mod}\" is not a valid modifier.", e);
                                    }
                                }
                            }
                            else
                            {
                                throw new JsonException (
                                                         $"Expected an array of modifiers, but got \"{reader.TokenType}\"."
                                                        );
                            }

                            break;

                        default:
                            throw new JsonException ($"Unexpected Key property \"{propertyName}\".");
                    }
                }
            }

            foreach (KeyCode modifier in modifiers)
            {
                key |= modifier;
            }

            return key;
        }

        throw new JsonException ($"Unexpected StartObject token when parsing Key: {reader.TokenType}.");
    }

    public override void Write (Utf8JsonWriter writer, KeyCode value, JsonSerializerOptions options)
    {
        writer.WriteStartObject ();

        var keyName = (value & ~KeyCode.CtrlMask & ~KeyCode.ShiftMask & ~KeyCode.AltMask).ToString ();

        if (keyName is { })
        {
            writer.WriteString ("Key", keyName);
        }
        else
        {
            writer.WriteNumber ("Key", (uint)(value & ~KeyCode.CtrlMask & ~KeyCode.ShiftMask & ~KeyCode.AltMask));
        }

        Dictionary<string, KeyCode> modifierDict = new ()
        {
            { "Shift", KeyCode.ShiftMask }, { "Ctrl", KeyCode.CtrlMask }, { "Alt", KeyCode.AltMask }
        };

        List<string> modifiers = new ();

        foreach (KeyValuePair<string, KeyCode> pair in modifierDict)
        {
            if ((value & pair.Value) == pair.Value)
            {
                modifiers.Add (pair.Key);
            }
        }

        if (modifiers.Count > 0)
        {
            writer.WritePropertyName ("Modifiers");
            writer.WriteStartArray ();

            foreach (string modifier in modifiers)
            {
                writer.WriteStringValue (modifier);
            }

            writer.WriteEndArray ();
        }

        writer.WriteEndObject ();
    }
}
