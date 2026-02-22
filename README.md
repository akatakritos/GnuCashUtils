# GnuCashUtils

My personal collection of utilties for working with GnuCash. Written in C# and Avalonia.

## Prereqisites

- You are using GnuCash with the sqlite file format
- Install the [dotnet 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## Getting Started

1. Clone this repository
2. Copy the `GnuCashUtils/config.yml.example` file to `~/.config/GnuCashUtils/config.yml` and adjust it to your needs
3. `cd GnuCashUtils`
4. `dotnet run`

If all goes well, this should open the GUI and let you use the tools.

## Tools

### Backup

Copies the sqlite file to a timestamped backup file in the same directory its stored in. You can then choose to open that file in GnuCashUtils
to test.

### Bulk Edit Account

View transactions in a source account and select which ones you would like to move to a different account.

For example, I once
decided I wanted to split `Expenses:Dining` into `Expenses:Dining:Quick Service` and `Expenses:Dining:Table Service`. I chose
`Expenses:Dining` as the source account and selected all the fast food joints, then picked `Expenses:Dining:Quick Service`
as the destination account.

This command only modifies the split that is selected as the source.

### Categorization

GnuCash does not tokenize descriptions well, and its bayesian categorizer thinks every variant of `AMAZON.COM/XKJG#129iX` has
never been seen before. This results in me having to manually apply the expense account every time.

This tool also does a Bayesian categorization of the description, but does a better job tokenizing and
removing noise.

Configure the CSV reading in the `config.yml` file. Right click on a row to set the account, or typeahead in the account column.

It saves a CSV file in a format compatible with easily importing to GnuCash with your categorizations applied.

### Tagger

Allows you to add tags to transactions. These are stored in the `notes` field of the transaction. Search for transactions by
date and description and edit the applied tags.

You can then run transaction reports by using the transaction filter feature inside GnuCash. 

The tags support names or names and values:

- `#[name]`
- `#[name=value]`

For example `#[business]` or `#[vacation=disney world]`. Spaces are allowed in the name or value.

You can filter in GnuCash by tags: `#[vacation` to find all vacation transactions regardless of which trip it was,
or `#[vacation=disney world]` to find all vacation transactions to Disney World.

Each transaction can have multiple tags.

## Contributing

Please open an issue before sending a PR. 

## LLM-based Disclosure

I've been using this project to experiment with LLM tools for development. It is **not** "vibe coded".
Each line of production code has been written or reviwed by me and I vouch for it. If the tool doesnt generate the code
the way I woudld have done it, I refactor, rewrite, or reprompt it. I'd say those parts were AI-typed rather than AI-written.

I expect the same from contributors.
