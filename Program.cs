using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CredentialManagement;
using Newtonsoft.Json;

namespace DriveMapper
{
    public class DriveMapping
    {
        public string Letter { get; set; }
        public string UncPath { get; set; }
        public string Username { get; set; }
    }

    public class Program
    {
        private static void ShowHelp(List<DriveMapping> mappings)
        {
            Console.WriteLine("Usage: DriveMapper.exe [map] [add] [delete]");
            if (mappings.Count > 0)
            {
                Console.WriteLine("Configured mappings:");
                foreach (var mapping in mappings)
                {
                    Console.WriteLine("{0}: -> {1} {2}", mapping.Letter, mapping.UncPath, mapping.Username);
                }
            }
        }

        static void Main(string[] args)
        {
            var vaultResourcePrefix = "DriveMapper-";
            var exePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            var configFileName = Path.Combine(exePath, "DriveMapper.json");
            var mappings = new List<DriveMapping>();
            if (File.Exists(configFileName))
            {
                mappings = JsonConvert.DeserializeObject<List<DriveMapping>>(File.ReadAllText(configFileName));
            }
            if (args.Length == 0)
            {
                ShowHelp(mappings);
                return;
            }

            if (args[0] == "add")
            {
                var letter = GetAvailableLetter(mappings);
                if (letter.Length == 0)
                {
                    Console.WriteLine("There are no available drive letters on your pc!");
                    return;
                }
                Console.Write("Enter letter with no colon (or accept {0} by pressing ENTER): ", letter);
                var enteredLetter = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(enteredLetter))
                {
                    letter = enteredLetter.ToUpper();
                }

                var uncPath = "";
                while (string.IsNullOrWhiteSpace(uncPath))
                {
                    Console.Write("Enter unc path: ");
                    uncPath = Console.ReadLine();
                }

                var username = "";
                while (string.IsNullOrWhiteSpace(username))
                {
                    Console.Write("Enter username: ");
                    username = Console.ReadLine();
                }

                var password = "";
                while (string.IsNullOrWhiteSpace(password))
                {
                    Console.Write("Enter password: ");
                    password = ReadPassword();
                }

                var mapping = new DriveMapping()
                {
                    Letter = letter.ToUpper(),
                    Username = username.ToLower(),
                    UncPath = uncPath.ToLower()
                };
                mappings.Add(mapping);
                File.WriteAllText(configFileName, JsonConvert.SerializeObject(mappings, Formatting.Indented));
                StoreCredential(vaultResourcePrefix + letter, username, password);
                Console.WriteLine();
                Console.WriteLine("Drive mapping added to config.");
            }
            else if (args[0] == "delete")
            {
                var letter = "";
                while (string.IsNullOrWhiteSpace(letter))
                {
                    Console.Write("Enter drive letter: ");
                    letter = Console.ReadLine().ToUpper();
                }
                var updatedMappings = new List<DriveMapping>();
                foreach (var mapping in mappings)
                {
                    if (letter != mapping.Letter)
                    {
                        updatedMappings.Add(mapping);
                    }
                }
                if (mappings.Count != updatedMappings.Count)
                {
                    File.WriteAllText(configFileName, JsonConvert.SerializeObject(updatedMappings, Formatting.Indented));
                    DeleteCredential(vaultResourcePrefix + letter);
                    Console.WriteLine("Drive mapping removed from config.");
                }
                else
                {
                    Console.WriteLine("Drive letter {0} not mapped.", letter);
                }
            }
            else if (args[0] == "map")
            {
                if (mappings.Count == 0)
                {
                    Console.WriteLine("No mapping configured.");
                    return;
                }
                foreach (var mapping in mappings)
                {
                    if (Directory.Exists(string.Format("{0}:", mapping.Letter)))
                    {
                        Console.WriteLine("Mapping {0}: -> {1}: already mapped", mapping.Letter, mapping.UncPath);
                    }
                    else
                    {
                        Console.Write("Mapping {0}: -> {1} - ", mapping.Letter, mapping.UncPath);
                        var password = RetrieveCredential(vaultResourcePrefix + mapping.Letter);
                        RunUsingShell("net use /persistent:no", ".");
                        var output = RunUsingShell(string.Format("net use {0}: {1} \"{2}\" /user:{3}", mapping.Letter, mapping.UncPath, password, mapping.Username), ".");
                        if (output.Contains("The command completed successfully."))
                        {
                            Console.WriteLine("ok");
                        }
                        else
                        {
                            Console.WriteLine(output);
                        }
                    }
                }
            }
            else
            {
                ShowHelp(mappings);
            }
        }

        private static string RunUsingShell(string commandWithArgs, string workingDirectory)
        {
            var outputFile = Path.GetTempFileName();
            var command = "cmd.exe";
            var args = "/c " + commandWithArgs + " > " + outputFile;
            var startInfo = new ProcessStartInfo(command, args)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var process = new Process() { StartInfo = startInfo };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (File.Exists(outputFile))
            {
                output = File.ReadAllText(outputFile);
                File.Delete(outputFile);
            }
            return output;
        }

        private static string GetAvailableLetter(List<DriveMapping> mappings)
        {
            // Create a collection of characters from 'Z' to 'A'
            IEnumerable<char> reverseAlphabet = GetReverseAlphabet();

            // Use a foreach loop to iterate through the collection
            foreach (char letter in reverseAlphabet)
            {
                var exists = false;
                foreach (var mapping in mappings)
                {
                    if (mapping.Letter == letter.ToString())
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    return letter.ToString();
                }
            }

            return "";
        }

        private static IEnumerable<char> GetReverseAlphabet()
        {
            for (char c = 'Z'; c >= 'A'; c--)
            {
                yield return c; // Yield each character in reverse order
            }
        }


        private static void StoreCredential(string resource, string username, string password)
        {
            var credential = new Credential
            {
                Target = resource,
                Username = username,
                Password = password,
                PersistanceType = PersistanceType.LocalComputer
            };
            credential.Save();
        }

        private static string RetrieveCredential(string resource)
        {
            var credential = new Credential { Target = resource };
            if (credential.Load())
            {
                return credential.Password;
            }
            else
            {
                return "";
            }
        }

        private static void DeleteCredential(string resource)
        {
            var credential = new Credential
            {
                Target = resource
            };

            if (credential.Exists())
            {
                credential.Delete();
            }
        }

        private static string ReadPassword()
        {
            string password = string.Empty;
            ConsoleKeyInfo keyInfo;

            do
            {
                keyInfo = Console.ReadKey(intercept: true); // Read key without displaying it
                if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    // Handle backspace
                    password = password.Substring(0, password.Length - 1); // Remove the last character
                    Console.Write("\b \b"); // Erase the last character in the console
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    // Append valid characters to password
                    password += keyInfo.KeyChar;
                    Console.Write("*"); // Display a masking character
                }
            } while (keyInfo.Key != ConsoleKey.Enter);

            return password;
        }
    }
}
