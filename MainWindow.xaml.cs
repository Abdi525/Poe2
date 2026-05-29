using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Poe2
{
    //Use a delegate to solve a programming problem
    public delegate void BotReplyHandler(string message);

    public partial class MainWindow : Window
    {
        private ChatbotEngine _chatbot;
        private SpeechSynthesizer _synthesizer;

        public MainWindow()
        {
            InitializeComponent();
            
            _chatbot = new ChatbotEngine(new MemoryManager(), new SentimentAnalyzer(), new KnowledgeBase());

            // Initialize Audio with Error Handling
            try
            {
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();
            }
            catch (Exception)
            {
                // Fails silently if no audio device is found to prevent crashing
                _synthesizer = null;
            }

            // Subscribe the delegate to our output methods
            _chatbot.OnBotReplied += DisplayBotMessage;
            _chatbot.OnBotReplied += SpeakBotMessage;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            WelcomeGrid.Visibility = Visibility.Collapsed;
            NameGrid.Visibility = Visibility.Visible;
            NameTextBox.Focus();
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            string cleanName = NameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(cleanName))
            {
                ErrorText.Text = "Name field cannot be left blank.";
                return;
            }

            if (!Regex.IsMatch(cleanName, @"^[a-zA-Z\s]+$"))
            {
                ErrorText.Text = "Please enter letters only.";
                return;
            }

            _chatbot.UserName = cleanName;

            NameGrid.Visibility = Visibility.Collapsed;
            ChatGrid.Visibility = Visibility.Visible;
            MessageBox.Focus();

            // Initialize conversation history flow
            string greeting = _chatbot.GetGreetingMessage();
            DisplayBotMessage(greeting);
            SpeakBotMessage(greeting);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            ProcessMessageTransmission();
        }

        private void MessageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProcessMessageTransmission();
            }
        }

        private void ProcessMessageTransmission()
        {
            string textInput = MessageBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(textInput)) return;

            // Display user message
            AppendLog(_chatbot.UserName, textInput, false);
            MessageBox.Clear();

            // Compute engine response
            _chatbot.ProcessUserMessage(textInput);
        }

        //Updates the UI
        private void DisplayBotMessage(string message)
        {
            AppendLog("Chatbot", message, true);
        }

        //Plays the Audio
        private void SpeakBotMessage(string message)
        {
            if (_synthesizer != null)
            {
                // Stop previous speech to avoid overlapping
                _synthesizer.SpeakAsyncCancelAll();
                _synthesizer.SpeakAsync(message);
            }
        }

        private void AppendLog(string speaker, string body, bool isBotMessage)
        {
            TextRange targetRange = new TextRange(ChatListBox.Document.ContentEnd, ChatListBox.Document.ContentEnd)
            {
                Text = $"{speaker}: {body}\n\n"
            };

            Brush textTheme = isBotMessage ? new SolidColorBrush(Color.FromRgb(122, 162, 247)) : new SolidColorBrush(Color.FromRgb(158, 206, 106));
            targetRange.ApplyPropertyValue(TextElement.ForegroundProperty, textTheme);
            targetRange.ApplyPropertyValue(TextElement.FontWeightProperty, isBotMessage ? FontWeights.Normal : FontWeights.SemiBold);

            ChatScroller.ScrollToEnd();
        }
    }

    public class ChatbotEngine
    {
        public string UserName { get; set; } = "Guest";
        private string _activeTopicTracking = "";

        public event BotReplyHandler OnBotReplied;

        private readonly MemoryManager _memory;
        private readonly SentimentAnalyzer _sentiment;
        private readonly KnowledgeBase _kb;

        public ChatbotEngine(MemoryManager memory, SentimentAnalyzer sentiment, KnowledgeBase kb)
        {
            _memory = memory;
            _sentiment = sentiment;
            _kb = kb;
        }

        public string GetGreetingMessage()
        {
            string savedInterests = _memory.RetrieveSavedTopic();
            string baseGreeting = $"Welcome to your AI security helper, {UserName}! How can I help protect your system today?";

            //Recall details later in the conversation
            if (!string.IsNullOrEmpty(savedInterests))
            {
                baseGreeting += $"\n\nI remember you were interested in '{savedInterests}'. You can ask me more about it, or ask a new question!";
            }
            return baseGreeting;
        }

        public void ProcessUserMessage(string rawInput)
        {
            string normalizedInput = rawInput.ToLower().Trim();
            string reply = "";

            //Memory and Recall
            if (normalizedInput.Contains("interested in"))
            {
                string extractedConcept = rawInput.Substring(rawInput.ToLower().IndexOf("interested in") + 13).Trim();
                if (string.IsNullOrEmpty(extractedConcept)) extractedConcept = "General Security";

                _memory.CommitTopicToStorage(extractedConcept);
                reply = $"Great! I'll remember that you're interested in {extractedConcept}. It's a crucial part of staying safe online.";
            }
            else
            {
                //Sentiment Detection
                string discoveredSentiment = _sentiment.ExtractSentimentTone(normalizedInput);
                string empathyResponsePrefix = _sentiment.GenerateEmpatheticSupport(discoveredSentiment, UserName);

                //Keyword Recognition
                string discoveredTopic = _kb.IdentifyTopicScope(normalizedInput);

                //Conversation Flow (Seamless Follow-ups)
                bool requiresMoreContext = normalizedInput.Contains("explain more") ||
                                           normalizedInput.Contains("more details") ||
                                           normalizedInput.Contains("tell me more") ||
                                           normalizedInput.Contains("another tip");

                if (string.IsNullOrEmpty(discoveredTopic) && requiresMoreContext && !string.IsNullOrEmpty(_activeTopicTracking))
                {
                    discoveredTopic = _activeTopicTracking; // Maintain conversational context
                }

                if (!string.IsNullOrEmpty(discoveredTopic))
                {
                    _activeTopicTracking = discoveredTopic;

                    //Random Responses
                    string informativeData = _kb.FetchTopicExplanation(discoveredTopic);
                    reply = !string.IsNullOrEmpty(empathyResponsePrefix) ? $"{empathyResponsePrefix} {informativeData}" : informativeData;
                }
                else if (!string.IsNullOrEmpty(discoveredSentiment))
                {
                    reply = $"{empathyResponsePrefix} Could you specify which threat you need help with? Try asking about passwords, phishing, or malware.";
                }
                else
                {
                    //Error Handling (Unknown Inputs)
                    reply = "I'm not sure I understand. Can you try rephrasing? I can help with topics like passwords, phishing, malware, or privacy.";
                }
            }

            // Invoke the delegate to send the response to the UI and Audio
            OnBotReplied?.Invoke(reply);
        }
    }

    public class SentimentAnalyzer
    {
        public string ExtractSentimentTone(string parsedText)
        {
            if (new[] { "worried", "anxious", "nervous", "scared", "hacked", "scam" }.Any(word => parsedText.Contains(word)))
                return "worried";

            if (new[] { "frustrated", "annoyed", "angry", "confused", "stuck" }.Any(word => parsedText.Contains(word)))
                return "frustrated";

            if (new[] { "curious", "wondering", "learn" }.Any(word => parsedText.Contains(word)))
                return "curious";

            return "";
        }

        public string GenerateEmpatheticSupport(string emotionalState, string profileName)
        {
            if (emotionalState == "worried")
                return $"It's completely understandable to feel that way, {profileName}. Scammers can be very convincing. Let me share some tips to help you stay safe.";

            if (emotionalState == "frustrated")
                return $"I hear you, {profileName}. Let's break this technical concept down carefully step-by-step to avoid any confusion.";

            if (emotionalState == "curious")
                return $"It's great that you want to learn more about this, {profileName}!";

            return "";
        }
    }

    public class MemoryManager
    {
        private readonly string _storagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "memory.txt");

        public void CommitTopicToStorage(string targetTopicText)
        {
            try { File.WriteAllText(_storagePath, targetTopicText); }
            catch { /* Silent catch for edge cases to maintain functionality */ }
        }

        public string RetrieveSavedTopic()
        {
            try
            {
                if (File.Exists(_storagePath)) return File.ReadAllText(_storagePath).Trim();
            }
            catch { }
            return "";
        }
    }

    public class KnowledgeBase
    {
        private readonly Random _rng = new Random();
        //Use dictionaries/lists/arrays to organise keyword responses
        private readonly Dictionary<string, string[]> _keywordDictionary;
        private readonly Dictionary<string, string[]> _repositories;

        public KnowledgeBase()
        {
            _keywordDictionary = new Dictionary<string, string[]>
            {
                { "phishing", new[] { "fake emails", "suspicious email", "phishing", "link", "scam" } },
                { "malware", new[] { "virus", "spyware", "malware", "trojan" } },
                { "passwords", new[] { "password", "login credentials" } },
                { "privacy", new[] { "privacy", "tracking", "personal data" } }
            };

            _repositories = new Dictionary<string, string[]>
            {
                { "phishing", new[] {
                    "Be cautious of emails asking for personal information. Scammers often disguise themselves as trusted organisations.",
                    "Phishing involves deceptive messaging. Always check email headers and don't click unknown links."
                }},
                { "malware", new[] {
                    "Malware is a catch-all term for malicious software. Ensure active security defenses stay patched weekly.",
                    "Only download software strictly from authorized, official vendor marketplaces."
                }},
                { "passwords", new[] {
                    "Make sure to use strong, unique passwords for each account. Avoid using personal details in your passwords.",
                    "Strong passwords use phrase constructs longer than 12 characters combining symbols and numbers."
                }},
                { "privacy", new[] {
                    "Data privacy configurations mitigate unauthorized tracking footprints across digital search trackers.",
                    "Minimize sharing identifiable artifacts publicly on web forums to prevent identity theft."
                }}
            };
        }

        public string IdentifyTopicScope(string processedInput)
        {
            foreach (var topic in _keywordDictionary)
            {
                if (topic.Value.Any(keyword => processedInput.Contains(keyword)))
                    return topic.Key;
            }
            return "";
        }

        public string FetchTopicExplanation(string targetTopic)
        {
            if (_repositories.ContainsKey(targetTopic))
            {
                string[] databaseList = _repositories[targetTopic];
                return databaseList[_rng.Next(databaseList.Length)];
            }
            return "";
        }
    }
}
