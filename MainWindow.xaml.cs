using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MySql.Data.MySqlClient;

namespace Poe2
{
    public delegate void BotReplyHandler(string message);

    public partial class MainWindow : Window
    {
        private ChatbotEngine _chatbot;
        private SpeechSynthesizer _synthesizer;

        public MainWindow()
        {
            InitializeComponent();

            _chatbot = new ChatbotEngine(new MemoryManager(), new SentimentAnalyzer(), new KnowledgeBase(), new DatabaseManager(), new ActivityLogger(), new QuizManager());

            try
            {
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();
            }
            catch (Exception)
            {
                _synthesizer = null;
            }

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

            AppendLog(_chatbot.UserName, textInput, false);
            MessageBox.Clear();

            _chatbot.ProcessUserMessage(textInput);
        }

        //Button Handlers
        private void Action_StartQuiz_Click(object sender, RoutedEventArgs e)
        {
            AppendLog(_chatbot.UserName, "Start quiz", false);
            _chatbot.ProcessUserMessage("start quiz");
        }

        private void Action_ViewTasks_Click(object sender, RoutedEventArgs e)
        {
            AppendLog(_chatbot.UserName, "View tasks", false);
            _chatbot.ProcessUserMessage("view tasks");
        }

        private void Action_ViewLog_Click(object sender, RoutedEventArgs e)
        {
            AppendLog(_chatbot.UserName, "Show activity log", false);
            _chatbot.ProcessUserMessage("show activity log");
        }

        private void DisplayBotMessage(string message)
        {
            AppendLog("Chatbot", message, true);
        }

        private void SpeakBotMessage(string message)
        {
            if (_synthesizer != null)
            {
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
        private readonly DatabaseManager _db;
        private readonly ActivityLogger _logger;
        private readonly QuizManager _quiz;

        public ChatbotEngine(MemoryManager memory, SentimentAnalyzer sentiment, KnowledgeBase kb, DatabaseManager db, ActivityLogger logger, QuizManager quiz)
        {
            _memory = memory;
            _sentiment = sentiment;
            _kb = kb;
            _db = db;
            _logger = logger;
            _quiz = quiz;

            _db.InitializeDatabase();
        }

        public string GetGreetingMessage()
        {
            string savedInterests = _memory.RetrieveSavedTopic();
            string baseGreeting = $"Welcome to your AI security helper, {UserName}! How can I help protect your system today?";

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

            
            if (_quiz.IsActive)
            {
                reply = _quiz.ProcessAnswer(normalizedInput, out bool isFinished);
                if (isFinished) _logger.LogAction("Quiz completed.");
                OnBotReplied?.Invoke(reply);
                return;
            }

            
            Match taskMatch = Regex.Match(normalizedInput, @"(?i)(?:add a task|set a reminder|remind me)(?: to)? (.+?)(?: (in \d+ days|tomorrow|next week))?$");
            if (taskMatch.Success)
            {
                string taskTitle = taskMatch.Groups[1].Value.Trim();
                string timeframe = taskMatch.Groups[2].Success ? taskMatch.Groups[2].Value : "no reminder set";

                reply = _db.AddTask(taskTitle, timeframe);
                _logger.LogAction($"Task added: '{taskTitle}' ({timeframe})");
                OnBotReplied?.Invoke(reply);
                return;
            }

            
            if (normalizedInput.Contains("show activity log") || normalizedInput.Contains("what have you done for me"))
            {
                _logger.LogAction("User viewed activity log.");
                reply = _logger.GetRecentLogs();
                OnBotReplied?.Invoke(reply);
                return;
            }
            if (normalizedInput.Contains("show more") || normalizedInput.Contains("older logs"))
            {
                reply = _logger.GetRecentLogs(true);
                OnBotReplied?.Invoke(reply);
                return;
            }
            if (normalizedInput.Contains("start quiz") || normalizedInput.Contains("play a game"))
            {
                _logger.LogAction("Quiz started.");
                reply = _quiz.StartQuiz();
                OnBotReplied?.Invoke(reply);
                return;
            }
            if (normalizedInput.Contains("view tasks") || normalizedInput.Contains("show tasks"))
            {
                reply = _db.GetTasks();
                OnBotReplied?.Invoke(reply);
                return;
            }

            // Task Deletion or Completion via Chat NLP
            Match deleteMatch = Regex.Match(normalizedInput, @"(?i)(?:delete|remove) task (\d+)");
            if (deleteMatch.Success)
            {
                reply = _db.DeleteTask(int.Parse(deleteMatch.Groups[1].Value));
                _logger.LogAction($"Task {deleteMatch.Groups[1].Value} deleted.");
                OnBotReplied?.Invoke(reply);
                return;
            }
            Match completeMatch = Regex.Match(normalizedInput, @"(?i)(?:complete|finish) task (\d+)");
            if (completeMatch.Success)
            {
                reply = _db.CompleteTask(int.Parse(completeMatch.Groups[1].Value));
                _logger.LogAction($"Task {completeMatch.Groups[1].Value} marked complete.");
                OnBotReplied?.Invoke(reply);
                return;
            }

            
            if (normalizedInput.Contains("interested in"))
            {
                string extractedConcept = rawInput.Substring(rawInput.ToLower().IndexOf("interested in") + 13).Trim();
                if (string.IsNullOrEmpty(extractedConcept)) extractedConcept = "General Security";

                _memory.CommitTopicToStorage(extractedConcept);
                reply = $"Great! I'll remember that you're interested in {extractedConcept}. It's a crucial part of staying safe online.";
            }
            else
            {
                string discoveredSentiment = _sentiment.ExtractSentimentTone(normalizedInput);
                string empathyResponsePrefix = _sentiment.GenerateEmpatheticSupport(discoveredSentiment, UserName);
                string discoveredTopic = _kb.IdentifyTopicScope(normalizedInput);

                bool requiresMoreContext = normalizedInput.Contains("explain more") || normalizedInput.Contains("more details");

                if (string.IsNullOrEmpty(discoveredTopic) && requiresMoreContext && !string.IsNullOrEmpty(_activeTopicTracking))
                {
                    discoveredTopic = _activeTopicTracking;
                }

                if (!string.IsNullOrEmpty(discoveredTopic))
                {
                    _activeTopicTracking = discoveredTopic;
                    string informativeData = _kb.FetchTopicExplanation(discoveredTopic);
                    reply = !string.IsNullOrEmpty(empathyResponsePrefix) ? $"{empathyResponsePrefix} {informativeData}" : informativeData;
                }
                else if (!string.IsNullOrEmpty(discoveredSentiment))
                {
                    reply = $"{empathyResponsePrefix} Could you specify which threat you need help with? Try asking about passwords, phishing, or malware.";
                }
                else
                {
                    reply = "I'm not sure I understand. You can ask me about cybersecurity, type 'start quiz', or say 'add a task to check my privacy settings'.";
                }
            }

            OnBotReplied?.Invoke(reply);
        }
    }

    // NEW FEATURES

    public class DatabaseManager
    {
        
        private readonly string connectionString = "Server=localhost;Database=CyberBotDB;Uid=root;Pwd=password;";

        public void InitializeDatabase()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection("Server=localhost;Uid=root;Pwd=password;"))
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand("CREATE DATABASE IF NOT EXISTS CyberBotDB;", conn);
                    cmd.ExecuteNonQuery();
                }

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string createTable = @"CREATE TABLE IF NOT EXISTS CyberTasks (
                                            Id INT AUTO_INCREMENT PRIMARY KEY,
                                            Title VARCHAR(255) NOT NULL,
                                            Timeframe VARCHAR(100),
                                            IsCompleted BOOLEAN DEFAULT FALSE
                                          );";
                    MySqlCommand cmd = new MySqlCommand(createTable, conn);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB Init Error (Ignored for UI flow): {ex.Message}");
            }
        }

        public string AddTask(string title, string timeframe)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO CyberTasks (Title, Timeframe) VALUES (@title, @time)";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@title", title);
                        cmd.Parameters.AddWithValue("@time", timeframe);
                        cmd.ExecuteNonQuery();
                    }
                }
                return $"Task added: '{title}'. Reminder set for: {timeframe}.";
            }
            catch
            {
                return "Error: Could not connect to the database to save your task. Please check MySQL.";
            }
        }

        public string GetTasks()
        {
            try
            {
                List<string> tasks = new List<string>();
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Id, Title, Timeframe FROM CyberTasks WHERE IsCompleted = FALSE";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tasks.Add($"[ID: {reader["Id"]}] {reader["Title"]} - Reminder: {reader["Timeframe"]}");
                        }
                    }
                }
                if (tasks.Count == 0) return "You currently have no pending tasks!";
                return "Here are your active tasks:\n" + string.Join("\n", tasks) + "\n\n(Type 'Complete task [ID]' or 'Delete task [ID]' to manage them).";
            }
            catch
            {
                return "Error: Could not retrieve tasks from the database.";
            }
        }

        public string CompleteTask(int id)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand("UPDATE CyberTasks SET IsCompleted = TRUE WHERE Id = @id", conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0 ? $"Task {id} marked as completed!" : $"Could not find a task with ID {id}.";
                }
            }
            catch { return "Database error occurred."; }
        }

        public string DeleteTask(int id)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand("DELETE FROM CyberTasks WHERE Id = @id", conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0 ? $"Task {id} deleted successfully." : $"Could not find a task with ID {id}.";
                }
            }
            catch { return "Database error occurred."; }
        }
    }

    public class ActivityLogger
    {
        private List<string> _logs = new List<string>();
        private int _currentOffset = 0;

        public void LogAction(string actionDescription)
        {
            _logs.Insert(0, $"[{DateTime.Now:HH:mm}] {actionDescription}");
            _currentOffset = 0;
        }

        public string GetRecentLogs(bool showMore = false)
        {
            if (_logs.Count == 0) return "Activity log is empty.";

            if (showMore) _currentOffset += 5;

            var paginatedLogs = _logs.Skip(_currentOffset).Take(5).ToList();

            if (paginatedLogs.Count == 0)
            {
                _currentOffset -= 5; // Revert if no more logs
                return "No older logs available.";
            }

            string result = "Here is a summary of recent actions:\n";
            for (int i = 0; i < paginatedLogs.Count; i++)
            {
                result += $"{i + 1 + _currentOffset}. {paginatedLogs[i]}\n";
            }

            if (_logs.Count > _currentOffset + 5)
            {
                result += "\nType 'show more' to see older actions.";
            }

            return result.TrimEnd();
        }
    }

    public class QuizManager
    {
        public bool IsActive { get; private set; } = false;
        private int _score = 0;
        private int _currentQuestionIndex = 0;

        private class Question
        {
            public string Text { get; set; }
            public string AnswerRegex { get; set; }
            public string Feedback { get; set; }
        }

        private readonly List<Question> _questions = new List<Question>
        {
            new Question { Text = "1. What should you do if you receive an email asking for your password?\nA) Reply\nB) Report as phishing\nC) Ignore", AnswerRegex = "^(b|report)", Feedback = "Correct! Reporting helps prevent scams." },
            new Question { Text = "2. True or False: 'password123' is a secure password.", AnswerRegex = "^(false|f)", Feedback = "Correct! That is highly insecure and easily guessed." },
            new Question { Text = "3. Which of the following is a type of malware?\nA) Firewall\nB) Trojan\nC) Antivirus", AnswerRegex = "^(b|trojan)", Feedback = "Spot on! A Trojan is disguised as legitimate software." },
            new Question { Text = "4. What does HTTPS mean?\nA) HyperText Transfer Protocol Secure\nB) Hidden Text Protocol", AnswerRegex = "^(a)", Feedback = "Correct! The 'S' stands for Secure." },
            new Question { Text = "5. True or False: You should use the same password for all accounts to remember them easily.", AnswerRegex = "^(false|f)", Feedback = "Correct! Always use unique passwords." },
            new Question { Text = "6. What is a 'Phishing' attack primarily trying to steal?\nA) Hardware\nB) Credentials\nC) Bandwidth", AnswerRegex = "^(b|credentials)", Feedback = "Right! They want your login info or personal data." },
            new Question { Text = "7. What is Two-Factor Authentication (2FA)?\nA) Using two passwords\nB) An extra layer of security requiring a second verification method", AnswerRegex = "^(b)", Feedback = "Yes! 2FA adds a massive layer of defense." },
            new Question { Text = "8. True or False: Public Wi-Fi is completely safe for online banking.", AnswerRegex = "^(false|f)", Feedback = "Correct! Public networks can easily be intercepted." },
            new Question { Text = "9. What is Ransomware?\nA) Software that speeds up PC\nB) Malware that encrypts your files and demands payment", AnswerRegex = "^(b)", Feedback = "Correct. Never pay the ransom if infected!" },
            new Question { Text = "10. How often should you update your software?\nA) Yearly\nB) When prompted or automatically", AnswerRegex = "^(b)", Feedback = "Correct! Updates contain vital security patches." },
            new Question { Text = "11. True or False: Incognito mode hides your activity from your Internet Service Provider.", AnswerRegex = "^(false|f)", Feedback = "Correct! It only hides history locally on your device." },
            new Question { Text = "12. Which character makes a password stronger?\nA) A space\nB) A special symbol like # or &", AnswerRegex = "^(b)", Feedback = "Right! Special characters drastically increase complexity." }
        };

        public string StartQuiz()
        {
            IsActive = true;
            _score = 0;
            _currentQuestionIndex = 0;
            return "Starting the Cybersecurity Quiz! (12 Questions)\n\n" + _questions[_currentQuestionIndex].Text;
        }

        public string ProcessAnswer(string input, out bool isFinished)
        {
            isFinished = false;
            string feedback = "";

            if (Regex.IsMatch(input.ToLower(), _questions[_currentQuestionIndex].AnswerRegex))
            {
                feedback = _questions[_currentQuestionIndex].Feedback;
                _score++;
            }
            else
            {
                feedback = "Incorrect. " + _questions[_currentQuestionIndex].Feedback;
            }

            _currentQuestionIndex++;

            if (_currentQuestionIndex >= _questions.Count)
            {
                IsActive = false;
                isFinished = true;
                string finalGrade = _score >= 10 ? "Great job! You're a cybersecurity pro!" : "Keep learning to stay safe online!";
                return $"{feedback}\n\n--- QUIZ COMPLETE ---\nYour final score: {_score}/{_questions.Count}\n{finalGrade}";
            }

            return $"{feedback}\n\nNext Question:\n{_questions[_currentQuestionIndex].Text}";
        }
    }

    public class SentimentAnalyzer
    {
        public string ExtractSentimentTone(string parsedText)
        {
            if (new[] { "worried", "anxious", "nervous", "scared", "hacked", "scam" }.Any(word => parsedText.Contains(word))) return "worried";
            if (new[] { "frustrated", "annoyed", "angry", "confused", "stuck" }.Any(word => parsedText.Contains(word))) return "frustrated";
            if (new[] { "curious", "wondering", "learn" }.Any(word => parsedText.Contains(word))) return "curious";
            return "";
        }

        public string GenerateEmpatheticSupport(string emotionalState, string profileName)
        {
            if (emotionalState == "worried") return $"It's completely understandable to feel that way, {profileName}. Scammers can be very convincing. Let me share some tips to help you stay safe.";
            if (emotionalState == "frustrated") return $"I hear you, {profileName}. Let's break this technical concept down carefully step-by-step to avoid any confusion.";
            if (emotionalState == "curious") return $"It's great that you want to learn more about this, {profileName}!";
            return "";
        }
    }

    public class MemoryManager
    {
        private readonly string _storagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "memory.txt");

        public void CommitTopicToStorage(string targetTopicText)
        {
            try { File.WriteAllText(_storagePath, targetTopicText); }
            catch { }
        }

        public string RetrieveSavedTopic()
        {
            try { if (File.Exists(_storagePath)) return File.ReadAllText(_storagePath).Trim(); }
            catch { }
            return "";
        }
    }

    public class KnowledgeBase
    {
        private readonly Random _rng = new Random();
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
                { "phishing", new[] { "Be cautious of emails asking for personal information. Scammers often disguise themselves as trusted organisations.", "Phishing involves deceptive messaging. Always check email headers and don't click unknown links." }},
                { "malware", new[] { "Malware is a catch-all term for malicious software. Ensure active security defenses stay patched weekly.", "Only download software strictly from authorized, official vendor marketplaces." }},
                { "passwords", new[] { "Make sure to use strong, unique passwords for each account. Avoid using personal details in your passwords.", "Strong passwords use phrase constructs longer than 12 characters combining symbols and numbers." }},
                { "privacy", new[] { "Data privacy configurations mitigate unauthorized tracking footprints across digital search trackers.", "Minimize sharing identifiable artifacts publicly on web forums to prevent identity theft." }}
            };
        }

        public string IdentifyTopicScope(string processedInput)
        {
            foreach (var topic in _keywordDictionary)
                if (topic.Value.Any(keyword => processedInput.Contains(keyword))) return topic.Key;
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
