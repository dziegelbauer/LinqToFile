# Documentation rules

## Reference

The documentation reference is ASD-STE100 Simplified Technical English, Issue 9, dated January 15, 2025.
The [official maintenance group](https://www.asd-ste100.org/) supplies the reference.
The repository does not redistribute the standard.

## Review

The documentation uses short sentences, active voice, and consistent technical terms.
Procedures give one instruction per sentence.
Descriptive paragraphs contain one topic.
Code identifiers keep their exact spelling.

A documentation review checks the applicable writing rules and dictionary.
A word-count check alone cannot establish STE100 compliance.
The project does not claim certification.

Code, commands, exception names, and quoted API identifiers are technical text.
License text remains unchanged.
Agent specifications are planning records rather than the user guide.

## Technical terms

| Term | Meaning in this project |
|---|---|
| Record | One logical CSV row, which can span physical lines |
| Header | The first logical record that names the columns |
| Field | One value within a record |
| Mapper | Generated code that constructs a typed record |
| Enumeration | One traversal of a source |
| Source generator | Compiler code that creates C# source |
| Materialization | Creation of records or a complete collection |
| Native AOT | Native ahead-of-time compilation |
| BCL | The .NET Base Class Library |

## Example checks

The README model and query match the executable sample.
The configured query in the CSV guide also matches the sample.
The scalar query in the conversion guide matches the sample.
The async and dictionary queries in the source guide match the sample.
The sample checks its result and returns a nonzero exit code on failure.
The test suite compiles generic helpers and checks the generated mapping contract.
The package check uses the same sample with a local package reference.
