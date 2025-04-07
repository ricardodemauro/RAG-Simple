# Simple LLM RAG: Console App

A console application that uses Retrieval Augmented Generation (RAG) to answer user questions based on processed text snippets.

## Description

This application processes a text file (e.g., `assets/book.txt`) to create a searchable database of content. Users can ask questions, and the application streams answers derived strictly from the context provided by the processed text. If the answer cannot be directly derived from the available context, the response will indicate uncertainty.

Based on: https://github.com/mtayyab2/RAG

## Technologies Used

- **Language:** C#
- **Platform:** .NET 9
- **Libraries:**
  - [OpenAI](https://www.nuget.org/packages/OpenAI/)
  - [Spectre.Console](https://spectreconsole.net/)
  - [Microsoft.Extensions.Configuration](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration)

## How It Works

1. **Initialization**:  
   - The application initializes necessary configurations and a database connection.
   - It starts by processing the text file located at `assets/book.txt`.

2. **Text Processing**:  
   - The `TextProcessor` reads the file and processes its content, preparing snippets to be used for context in question answering.

3. **Question & Answer Flow**:  
   - The console prompts the user to input a question.
   - The question, along with the context derived from the processed text, is sent to the LLM service.
   - The LLM answers the question using only the provided context and streams the response back to the console.
   - Users can repeatedly ask questions or type `exit` to close the application.

## How to Play

1. **Prerequisites**
   - .NET 9 SDK
   - Visual Studio 2022 or any compatible IDE is recommended.

2. **Setup and Installation**
   - Clone the repository: `https://github.com/ricardodemauro/RAG-Simple`
   - Navigate to the project directory: `cd llm-rag-console-app`
   - Restore dependencies: `dotnet restore`

3. **Running the Application**
   - Build the project: `dotnet build`
   - Run the application: `dotnet run`
   - Follow the instructions in the console:
   - Enter a question when prompted.
   - View the answer as it streams in.
   - Type `exit` to terminate the app.

## Summary

This LLM RAG Console App combines text processing, a searchable context database, and a powerful LLM to provide concise and accurate answers to user questions based entirely on supplied content. Enjoy using the app, and feel free to contribute or suggest improvements!
