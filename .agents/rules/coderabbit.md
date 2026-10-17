# CodeRabbit Automated Pre-Commit Review & Auto-Fix Workflow

When instructed to commit code, execute the commit via the terminal.

## Pre-Commit Review Loop:
If the pre-commit hook fails or CodeRabbit reports issues:
1. Read the `.cr-review-output.md` file in the root directory.
2. Analyze the feedback, suggestions, and warnings.
3. Automatically implement the required fixes in the respective source files.
4. Run `git add` for the modified files.
5. Re-run `git commit` with the original commit message.
6. Repeat this process autonomously until the commit succeeds.
