# Tinder

Tinder will be a programming language intended for cross-platform application development. It sits between languages like C++, Java, C#, Python, Objective-C, and JavaScript and can be compiled down to readable code in all of those languages. It is meant to be used for the majority of cross-platform codebases, with a little bit of target-specific glue code for bindings to subsystems such as UI or networking. Note that Tinder is an experiment and is not yet ready for actual use.

An example Tinder program:

    external {
      void print(string text)
      string toString(int num)
    }

    void main() {
      list<int> nums = [1, 2, 3]
      int i = 0
      while i < 3 {
        print("hello, " + toString(nums[i]))
        i = i + 1
      }
    }

## Features

* Nullable types
* Minimal, curly-braced syntax
* Simple design, compilable to JavaScript, C++, and other targets
* Advanced dataflow analysis for local nullable types

## Current Compiler

The current compiler was implemented in C# as a project for the course cs126 at Brown University in 2012. When run in MonoDevelop, it launches a server at [http://localhost:8080/](http://localhost:8080/) with an interactive compiler. Code typed in the textarea will be recompiled after every keystroke. The compiler currently generates valid JavaScript and mostly valid C++ (classes are not yet reordered to always come before their use, for example).

## Wiki Pages

* [Compiler Details](http://github.com/evanw/tinder/wiki/Compiler-Details)
* [Language Reference](http://github.com/evanw/tinder/wiki/Language-Reference)
