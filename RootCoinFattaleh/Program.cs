using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using EllipticCurve;


namespace RootCoinFattaleh
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // keys

            PrivateKey key1 = new PrivateKey();
            PublicKey wallet1 = key1.publicKey();

            PrivateKey key2 = new PrivateKey();
            PublicKey wallet2 = key2.publicKey();

            BlockChain rootcoin = new BlockChain(2,100);


            Console.WriteLine("START THE MINER");
            rootcoin.MinePendingTransactions(wallet1);
            Console.WriteLine("\n balance of walle1 is: " + rootcoin.GetBalanceOfWallet(wallet1).ToString());


            Transaction tx1 = new Transaction(wallet1,wallet2,10);
            tx1.SignTransaction(key1);
            rootcoin.addPendingTransaction(tx1);

            Console.WriteLine("START THE MINER");
            rootcoin.MinePendingTransactions(wallet2);
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine("\n balance of walle1 is: " + rootcoin.GetBalanceOfWallet(wallet1).ToString());
            Console.WriteLine("\n balance of walle2 is: " + rootcoin.GetBalanceOfWallet(wallet2).ToString());



            //Block newBlock = new Block(1, DateTime.Now.ToString("yyyyMMddmmssffff"), "amount: 50");
            //Block newBlock2 = new Block(2, DateTime.Now.ToString("yyyyMMddmmssffff"), "amount: 200");

            //rootcoin.AddBlock(newBlock);
            //rootcoin.AddBlock(newBlock2);
            string blockJson = JsonConvert.SerializeObject(rootcoin, Formatting.Indented);
            Console.WriteLine(blockJson);

            // check if valid

           // rootcoin.GetLatestBlock().PreviousHash = "12345";

            if (rootcoin.IsChainValid())
            {
                Console.WriteLine("BLOCK CHAIN IS VALID");
            }
            else
            {
                Console.WriteLine("Block CHain is not Valid");
            }
            
        }

        /* public static IHostBuilder CreateHostBuilder(string[] args) =>
             Host.CreateDefaultBuilder(args)
                 .ConfigureWebHostDefaults(webBuilder =>
                 {
                     webBuilder.UseStartup<Startup>();
                 });*/


        public class Block
        {
            public int Index { get; set; }
            public string PreviousHash { get; set; }
            public string TimeStamp { get; set; }
            public string Hash { get; set; }
            public int Nonce { get; set; }
            public List<Transaction> Transactions { get; set;}

            public Block(int index, string time_stamp, List<Transaction> transactions, string previoushash = "")
            {
                this.Index = index;
                this.PreviousHash = previoushash;
                this.Transactions = transactions;
                this.TimeStamp = time_stamp;
                this.Hash = calculateHash();
                this.Nonce = 0;
            }

            public string calculateHash()
            {
                string blockData = this.Index + this.PreviousHash + this.TimeStamp + this.Transactions.ToString() + this.Nonce;
                byte[] blockArray = Encoding.ASCII.GetBytes(blockData);
                byte[] hashBytes = SHA256.Create().ComputeHash(blockArray);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }

            public void Mine(int difficulty)
            {
                while(this.Hash.Substring(0,difficulty) != new String('0', difficulty))
                {
                    this.Nonce++;
                    this.Hash = this.calculateHash();
                    Console.WriteLine("Mining: " + this.Hash);
                }

                Console.WriteLine("Block has been Mined " + this.Hash);
            }
        }

        public class BlockChain
        {
            public List<Block> Chain { get; set; }
            public int Difficulty { get; set; }

            public List<Transaction> pendingTransaction { get; set; }
            public decimal MiningReward { get; set; }

            public BlockChain(int difficulty,decimal miningReward)
            {
                this.Chain = new List<Block>();
                this.Chain.Add(CreateGensisBlock());
                this.Difficulty = difficulty;
                this.MiningReward = miningReward;
                this.pendingTransaction = new List<Transaction>();

            }

            public Block CreateGensisBlock()
            {
                return new Block(0, DateTime.Now.ToString("yyyyMMddmmssffff"),new List<Transaction>());

            }

            public Block GetLatestBlock()
            {
                return this.Chain.Last();
            }

            public void AddBlock(Block newBlock)
            {
                newBlock.PreviousHash = this.GetLatestBlock().Hash;
                newBlock.Hash = newBlock.calculateHash();
                this.Chain.Add(newBlock);
            }

            public void addPendingTransaction(Transaction transaction) {
                if(transaction.FromAdress is null || transaction.ToAdress is null)
                {
                    throw new Exception("Transaction must have a  to and form address");
                }

                if(transaction.Amount > this.GetBalanceOfWallet(transaction.FromAdress))
                {
                    throw new Exception("Insufficent funds in wallet");
                }
                if(transaction.IsValid() == false)
                {
                    throw new Exception("Cannot add invalid transaction to block");
                }

                this.pendingTransaction.Add(transaction);
            }

            public decimal GetBalanceOfWallet(PublicKey address)
            {
                decimal balance = 0;

                string adressDER = BitConverter.ToString(address.toDer()).Replace("-", "");
                

                foreach (Block block in this.Chain)
                {
                    foreach(Transaction transaction in block.Transactions)
                    {
                     
                        if (!(transaction.FromAdress is null))
                        {
                            string fromDER = BitConverter.ToString(transaction.FromAdress.toDer()).Replace("-", "");
                            if (fromDER == adressDER)
                            {
                                balance -= transaction.Amount;
                            }
                        }
                        string toDER = BitConverter.ToString(transaction.ToAdress.toDer()).Replace("-", "");
                        if (toDER == adressDER)
                        {
                            balance += transaction.Amount;
                        }
                    }

                }
                return balance;
            }

            public void MinePendingTransactions(PublicKey MiningRewardWallet)
            {
                Transaction rewardTx = new Transaction(null, MiningRewardWallet, MiningReward);
                this.pendingTransaction.Add(rewardTx);

                Block newBlock = new Block(GetLatestBlock().Index + 1, DateTime.Now.ToString("yyyyMMddmmssffff"), this.pendingTransaction, GetLatestBlock().Hash);
                newBlock.Mine(this.Difficulty);

                Console.WriteLine("Block Successfully Mined!");
                this.Chain.Add(newBlock);
                this.pendingTransaction = new List<Transaction>();
            }

            public bool IsChainValid()
            {
                for (int i = 1; i < this.Chain.Count; i++)
                {
                    Block currentBlock = this.Chain[i];
                    Block previousBlock = this.Chain[i - 1];
                    // check if current hash is same as calculated hash
                    if (currentBlock.Hash != currentBlock.calculateHash())
                    {
                        //problem
                        return false;
                    }
                    if (currentBlock.PreviousHash != previousBlock.Hash)
                    {
                        //problem
                        return false;
                    }
                }
                return true;
            }
        }

        public class Transaction
        {
            public PublicKey FromAdress { get; set; }
            public PublicKey ToAdress { get; set; }
            public decimal Amount { get; set; }
            public Signature Signature { get; set; }
        

            public Transaction(PublicKey fromAdress, PublicKey toAdress,decimal amount)
            {
                this.FromAdress = fromAdress;
                this.ToAdress = toAdress;
                this.Amount = amount;
            }

            public void SignTransaction(PrivateKey signingKey)
            {
                string fromAdressDER = BitConverter.ToString(FromAdress.toDer()).Replace("-", "");
                string signingDER = BitConverter.ToString(signingKey.publicKey().toDer()).Replace("-", "");

                if(fromAdressDER != signingDER)
                {
                    //problem
                    throw new Exception("You cannot sign transaction for other wallet");
                }

                string txHash = this.CalculateHash();
                this.Signature = Ecdsa.sign(txHash, signingKey);

            }

            public string CalculateHash()
            {
                string fromAdressDER = BitConverter.ToString(FromAdress.toDer()).Replace("-", "");
                string toAdressDER = BitConverter.ToString(ToAdress.toDer()).Replace("-", "");
                string transactionData = fromAdressDER + toAdressDER + Amount;
                byte[] tdBytes = Encoding.ASCII.GetBytes(transactionData);
                return BitConverter.ToString(SHA256.Create().ComputeHash(tdBytes)).Replace("-", "");

            }

            public bool IsValid()
            {
                if (this.FromAdress is null) return true;

                if (this.Signature is null)
                {
                    throw new Exception("No Signature");
                };

                return Ecdsa.verify(this.CalculateHash(), this.Signature, this.FromAdress);

            }



    }


    }
}
