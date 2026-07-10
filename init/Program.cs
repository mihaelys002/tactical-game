bool debugAI = System.Array.IndexOf(args, "--debug-ai") >= 0;
bool visionAI = System.Array.IndexOf(args, "--vision-ai") >= 0;
TacticalGame.HeadlessBattle.Run(debugAI: debugAI, visionAI: visionAI);
