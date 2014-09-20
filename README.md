DependencyInjection.Annotations
===============================
A proof of concept where Services are mapped using attributes.

Instead of needing to enumerate over the assembly at runtime, you can enumerate over the source with Roslyn at Compile time, and build a list of static method calls to be emitted.

This is very much a prototype, but I would like to enhance it a little further, and make it easier to consume / use.
