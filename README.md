![Icon](https://raw.githubusercontent.com/Licshee/Dutiful.Fody/master/Timeline.png)

### This is an add-in for [Fody](https://github.com/Fody/Fody/) 

Enables chaining for your instance methods.

## The nuget package  [![NuGet Status](http://img.shields.io/nuget/v/Dutiful.Fody.svg?style=flat)](https://www.nuget.org/packages/Dutiful.Fody/)

https://nuget.org/packages/Dutiful.Fody/

    PM> Install-Package Dutiful.Fody

## Your Code

    public class Program
    {
      public int Main(string[] args)
      {
        /* do your thing */
      }
    }

## What's get compiled

    public class Program
    {
      public int Main(string[] args)
      {
        /* do your thing */
      }
      public Program MainDutiful(string[] args)
      {
        Main(args);
        return this;
      }
    }

# How it selects methods

* Only public, protected and protected internal methods will get Dutiful wrappers.
* Instance methods will get processed, static methods and constructors will not.
* Interfaces, enums are ignored.
* Methods original declared on System.Object are ignored by default.
* Accessors of properties and events are ignored.

## Icon

<a href="https://thenounproject.com/term/timeline/214157/" target="_blank">Timeline</a> designed by <a href="https://thenounproject.com/olsjoe" target="_blank">Joel Olson</a> from The Noun Project
