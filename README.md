Poe 1
I had created an interactive console application in which I can educate users on digital safety measures.
It also has and audio engagment and animated text.
I have also made it color coded to make it much interesting than using one color.
If the audio file is missing it will show an error, but the text-based section will function independedly.

POE 2  
I have created a chatbot that has a dictionary to store input related cyber security. 
Intergrated part 1 into part 2.
Both my ASCII from part 1 and my logo are visible.
Will detect if there is a need for a sentiment in the response.
Handles errors much better.

Poe 3
1. Robust MySQL Database Integration: Added a DatabaseManager class that handles all CRUD operations (Create, Read, Update, Delete) for tasks. It includes comprehensive error handling so the app won't crash if the database is offline; it will gracefully notify the user in the chat.
2. Advanced NLP Simulation: The chatbot now uses Regular Expressions (Regex) and string manipulation to detect intent. It can understand variations like "Remind me to update my password in 3 days", "Add a task to check firewall", or "Show activity log".
3. Comprehensive Activity Log: An ActivityLogger tracks every significant action (tasks added, quizzes started, NLP commands recognized) with timestamps. It supports pagination, showing the last 5 actions and allowing the user to type "show more" to see previous history.
4. 12-Question Cybersecurity Mini-Game: The QuizManager now features 12 varied questions (Multiple Choice and True/False). It traps the user in a "quiz state" until finished, provides immediate detailed feedback per question, and calculates a final score with personalized feedback.
5. Seamless GUI Integration: The chat interface handles all new features naturally. I also added quick-action buttons to the UI to emphasize the GUI aspect of the rubric, allowing users to trigger the log or quiz without typing
