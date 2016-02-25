![Icon](https://raw.githubusercontent.com/Licshee/Dutiful.Fody/master/Timeline.png)

### This is an add-in for [Fody](https://github.com/Fody/Fody/) 

Enables chaining/cascading for your instance methods (AKA Fluent API).

## The nuget package  [![NuGet Status](http://img.shields.io/nuget/v/Dutiful.Fody.svg?style=flat)](https://www.nuget.org/packages/Dutiful.Fody/)

https://nuget.org/packages/Dutiful.Fody/

    PM> Install-Package Dutiful.Fody

## What it does

### Your Code

    public class Program
    {
      public int Main(string[] args)
      {
        /* do your thing */
      }
    }

### What's get compiled

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

## How about Task?

You have to explicitly specify your the name pattern you want for synchronized wrappers first:

    <Dutiful SyncNameFormat="Sync"/>

### Then Your Code

    public class Program
    {
      public async Task GetTask()
      {
        /* do your thing */
      }
    }

### ... is compiled to...

    public class Program
    {
      public async Task GetTask()
      {
        /* do your thing */
      }
      public void GetTaskSync()
      {
        AsyncContext.Run(GetTask);
      }
      public void GetTaskSyncDutiful()
      {
        GetTaskSync();
        return this;
      }
    }
    
**Currently only methods can cast to `Func<Task>` are supported.**

## How it selects methods

* Only public, protected and protected internal methods will get Dutiful wrappers.
* Instance methods will get processed, static methods and constructors will not.
* Interfaces, enums are ignored, structs are ignored by default.
* If generating wrapper for a method can cause ambiguity, then it skips.
* Methods original declared on System.Object are ignored by default.
* Methods returning same type as what they belong to are ignored.
* Accessors of properties and events are ignored.

## Configuration

### Change nameing pattern

If you want methods of structs also get their Dutiful form, do this:

    <Dutiful TargetLevel="Struct"/>

To change the default suffix for generated methods, you do this:

    <Dutiful NameFormat="Fluent"/>

If you want prefix, you do this:

    <Dutiful NameFormat="Do*Fluently"/>

or this:

    <Dutiful NameFormat="Do{0}Fluently"/>

### Stop Words

`<TBD>`

## Icon

<a href="https://thenounproject.com/term/timeline/214157/" target="_blank">Timeline</a> designed by <a href="https://thenounproject.com/olsjoe" target="_blank">Joel Olson</a> from The Noun Project
