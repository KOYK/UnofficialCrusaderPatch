﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace UCPAIConversion
{
    class AISerializer : JavaScriptConverter
    {
        private readonly Dictionary<string, string> errorMessages;
        private readonly Dictionary<string, string> errorHints;

        private AICSerializationException AICSerializationExceptionList = new AICSerializationException();

        public AISerializer (Dictionary<String, String> errorMessages, Dictionary<String, String> errorHints)
        {
            this.errorMessages = errorMessages;
            this.errorHints = errorHints;
        }

        private String GetErrorMessage(String field, String character)
        {
            String msg = "Error parsing field " + field + ".";
            if (this.errorMessages.ContainsKey(field))
            {
                msg += " Reason: " + this.errorMessages[field];
            }
            if (this.errorHints.ContainsKey(field))
            {
                msg += "\nHint: " + this.errorHints[field];
            }
            return msg;
        }

        public override IEnumerable<Type> SupportedTypes
        {
            get { return new ReadOnlyCollection<Type>(new List<Type>(new Type[] { typeof(AICollection) })); }
        }

        public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            List<AICharacter> AICharacters = new List<AICharacter>();
            AICollection collection = new AICollection();

            Type AICharacterType = typeof(AICharacter);
            Type AIPersonalityType = typeof(AIPersonality);

            foreach (KeyValuePair<string, object> entry in dictionary)
            {
                AICharacterSerializationException SerializationErrors = new AICharacterSerializationException();
                SerializationErrors.Errors = new List<string>();

                if (entry.Key == "AICShortDescription")
                {
                    foreach (KeyValuePair<string, object> field in (Dictionary<string, object>)entry.Value)
                    {
                        try
                        {
                            header[field.Key] = field.Value.ToString().Substring(0, Math.Min(field.Value.ToString().Length, 1000)).ToString();
                        }
                        catch (ArgumentException)
                        {
                            SerializationErrors.Errors.Add(GetErrorMessage(field.Key, null));
                        }
                    }

                } else if (entry.Key == "AICharacters")
                {
                    AICharacter currentCharacter;
                    AIPersonality currentPersonality;
                    foreach (var character in (System.Collections.ArrayList)entry.Value)
                    {
                        currentCharacter = new AICharacter();
                        SerializationErrors = new AICharacterSerializationException();

                        foreach (KeyValuePair<string, object> definition in (Dictionary<string, object>)character)
                        {
                            if (definition.Key != "Personality")
                            {
                                try
                                {
                                    SetProperty(AICharacterType, currentCharacter, definition.Key, definition.Value);
                                }
                                catch (Exception e)
                                {
                                    if (e is TargetInvocationException || e is ArgumentException)
                                    {
                                        String customName = currentCharacter.CustomName;
                                        String name = (currentCharacter._Name != 0) ? null : Enum.GetName(typeof(AICharacterName), currentCharacter._Name);
                                        String errorName = ((name == null || name.Equals(String.Empty) || customName.Equals(name)) ? String.Empty : " (" + customName + ")");
                                        SerializationErrors.Errors.Add(GetErrorMessage(definition.Key, errorName));
                                    }
                                }

                            } else if (definition.Key == "Personality")
                            {
                                currentPersonality = new AIPersonality();
                                foreach (KeyValuePair<string, object> personalityValue in (Dictionary<string, object>)definition.Value)
                                {
                                    if (personalityValue.Key.ToLowerInvariant().Contains("description") ||
                                        personalityValue.Key.ToLowerInvariant().Contains("comment"))
                                    {
                                        continue;
                                    }
                                    try
                                    {
                                        SetProperty(AIPersonalityType, currentPersonality, personalityValue.Key, personalityValue.Value);
                                    }
                                    catch (ArgumentException)
                                    {
                                        String customName = currentCharacter.CustomName;
                                        String name = (currentCharacter._Name != 0) ? null : Enum.GetName(typeof(AICharacterName), currentCharacter._Name);
                                        String errorName = ((name == null || name.Equals(String.Empty) || customName.Equals(name)) ? String.Empty : " (" + customName + ")");
                                        SerializationErrors.Errors.Add(GetErrorMessage(personalityValue.Key, errorName));
                                    }
                                    catch (NullReferenceException)
                                    {
                                        String customName = currentCharacter.CustomName;
                                        String name = (currentCharacter._Name != 0) ? null : Enum.GetName(typeof(AICharacterName), currentCharacter._Name);
                                        String errorName = ((name == null || name.Equals(String.Empty) || customName.Equals(name)) ? String.Empty : " (" + customName + ")");
                                        SerializationErrors.Errors.Add(GetErrorMessage(personalityValue.Key, errorName));
                                    }
                                    catch (Exception)
                                    {
                                        String customName = currentCharacter.CustomName;
                                        String name = (currentCharacter._Name != 0) ? null : Enum.GetName(typeof(AICharacterName), currentCharacter._Name);
                                        String errorName = ((name == null || name.Equals(String.Empty) || customName.Equals(name)) ? String.Empty : " (" + customName + ")");
                                        SerializationErrors.Errors.Add(GetErrorMessage(personalityValue.Key, errorName));
                                    }
                                }
                                currentCharacter.Personality = currentPersonality;
                            }
                        }
                        AICharacters.Add(currentCharacter);
                        if (SerializationErrors.Errors.Count > 0 || currentCharacter._Name == 0)
                        {
                            String customName = currentCharacter.CustomName;
                            String name = (currentCharacter._Name != 0) ? null : Enum.GetName(typeof(AICharacterName), currentCharacter._Name);
                            String errorName = ((name == null || customName.Equals(String.Empty) || customName.Equals(name)) ? String.Empty : " (" + customName + ")");
                            SerializationErrors.AssociatedAICharacter = errorName;

                            if (currentCharacter._Name == 0)
                            {
                                SerializationErrors.Errors.Add(GetErrorMessage("Index", errorName));
                            }

                            AICSerializationExceptionList.ErrorList.Add(SerializationErrors);
                        }
                    }
                }
            }
            if (AICSerializationExceptionList.ErrorList.Count > 0)
            {                
                throw AICSerializationExceptionList;
            }
            collection.AICShortDescription = header;
            collection.AICharacters = AICharacters;
            return collection;
        }

        public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private void SetProperty(Type targetType, object target, String expectedFieldName, object expectedFieldValue)
        {
            if (expectedFieldName == "Unknown131")
            {
                expectedFieldName = "AttUnitPatrolRecommandDelay";
            }
            PropertyInfo parameter = targetType.GetProperty(expectedFieldName);
            if (parameter.PropertyType == typeof(bool))
            {
                parameter.SetValue(target, Convert.ToBoolean(expectedFieldValue), null);
            }
            else
            {
               try
                {
                    parameter.SetValue(target, expectedFieldValue, null);
                }
                catch (ArgumentException e)
                {
                    if (typeof(AIPersonality).GetProperty("_" + expectedFieldValue) != null)
                    {
                        PropertyInfo enumParam = typeof(AIPersonality).GetProperty("_" + expectedFieldValue);
                        enumParam.SetValue(target, expectedFieldValue, null);
                    } else
                    {
                        throw e;
                    }
                }
            }
        }
    }
}
