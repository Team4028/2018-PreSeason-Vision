# 2018-PreSeason-Vision
OpenCV Vision Server PreSeason Work

This is a re-implementation of the 2017 Season Image Recognition Solution used by Team 4028 for Steamworks.

Our goal was to develop a new solution that overcame the stability and operational shortcoming of the previous solution.

This codebase contains the following features:

- Runs as either a console application or a Windows Service
- Fully configurable via a JSON config file read at startup
- Leverages multiple threads to improve overall performance
- Supports a high (20) FPS at a high resolution (1024x768)
- Uses OpenCV v3.3 to analyze Camera Images in real-time
- Supports two (2) options to supply data to the RoboRIO
	- TCP Socket Data Server
	- Network Tables Client
- MPEG Streaming Image Server to display live images on the dashboard
	- supports downsizing from raw camera resolution to reduce bandwidth requirements
- USB Blinkstick LED Status indicator

The source code is written in C# and requires the .Net Framework v4.6.1

You must have a version of Visual Studio 2017 to build the solution.

The target platform is a small, on-board, fan-less, mini pc running Windows 10

Here is the proposed hardware layout:

![](https://github.com/Team4028/2018-PreSeason-Vision/blob/master/Images/Proposed%20Hardware%20Layout%20(resize).jpg)