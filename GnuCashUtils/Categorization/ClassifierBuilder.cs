using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.Core;

namespace GnuCashUtils.Categorization;

public interface IClassifierBuilder
{
    IObservable<double> Progress { get; }
    IObservable<ClassifierBuilder.BuilderStatus> Status { get; }
    Task<NaiveBayesianClassifier> Build(string primaryAccountGuid, CancellationToken cancellationToken);
}
public class ClassifierBuilder: IClassifierBuilder
{
    public enum BuilderStatus
    {
        Idle,
        Running,
    }

    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IAccountStore _accountStore;

    private readonly BehaviorSubject<double> _progress = new(0);
    public IObservable<double> Progress => _progress;

    private readonly BehaviorSubject<BuilderStatus> _status = new(BuilderStatus.Idle);
    public IObservable<BuilderStatus> Status => _status;


    public ClassifierBuilder(IDbConnectionFactory dbConnectionFactory, IAccountStore accountStore)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _accountStore = accountStore;
    }

    private void Cancel(CancellationToken cancellationToken)
    {
        _status.OnNext(BuilderStatus.Idle);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task<NaiveBayesianClassifier> Build(string primaryAccountGuid, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            _status.OnNext(BuilderStatus.Running);
            using var conn = _dbConnectionFactory.GetConnection();
            var classifier = new NaiveBayesianClassifier(new Tokenizer());

            if (cancellationToken.IsCancellationRequested)
            {
                Cancel(cancellationToken);
            }

            var transactionCount =
                conn.ExecuteScalar<int>("select count(*) from splits where account_guid = @accountGuid",
                    new { accountGuid = primaryAccountGuid });

            _progress.OnNext(0);

            using var reader = conn.ExecuteReader(@"
            select transactions.guid, transactions.description, accounts.guid as account_guid, splits.value_num, splits.value_denom
from transactions
         join splits on splits.tx_guid = transactions.guid and splits.account_guid <> @accountGuid
         join accounts on accounts.guid = splits.account_guid
where exists (select 1 from splits where splits.tx_guid = transactions.guid and splits.account_guid = @accountGuid)
", new { accountGuid = primaryAccountGuid });


            var processed = 0;
            while (reader.Read())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Cancel(cancellationToken);
                }
                var description = Convert.ToString(reader["description"])!;
                var accountGuid = Convert.ToString(reader["account_guid"])!;
                var numerator = Convert.ToInt32(reader["value_num"]);
                var denominator = Convert.ToInt32(reader["value_denom"]);
                var value = numerator / (decimal)denominator;

                classifier.Train(description!, value, _accountStore.Accounts.Lookup(accountGuid).Value.FullName);
                processed++;
                _progress.OnNext(processed / (double)transactionCount);

            }

            _status.OnNext(BuilderStatus.Idle);
            _progress.OnNext(1);
            return classifier;
        }, cancellationToken);
    }
}