/**
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2026, Sjofn LLC
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names 
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
 * POSSIBILITY OF SUCH DAMAGE.
 */

using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    /// <summary>
    /// AgentManager partial class - Money
    /// </summary>
    public partial class AgentManager
    {
        #region Money

        /// <summary>
        /// Request the current L$ balance
        /// </summary>
        public void RequestBalance()
        {
            MoneyBalanceRequestPacket money = new MoneyBalanceRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                MoneyData = {TransactionID = UUID.Zero}
            };

            Client.Network.SendPacket(money);
        }

        /// <summary>
        /// Give Money to destination Avatar
        /// </summary>
        /// <param name="target">UUID of the Target Avatar</param>
        /// <param name="amount">Amount in L$</param>
        public void GiveAvatarMoney(UUID target, int amount)
        {
            GiveMoney(target, amount, string.Empty, MoneyTransactionType.Gift, TransactionFlags.None);
        }

        /// <summary>
        /// Give Money to destination Avatar
        /// </summary>
        /// <param name="target">UUID of the Target Avatar</param>
        /// <param name="amount">Amount in L$</param>
        /// <param name="description">Description that will show up in the
        /// recipients transaction history</param>
        public void GiveAvatarMoney(UUID target, int amount, string description)
        {
            GiveMoney(target, amount, description, MoneyTransactionType.Gift, TransactionFlags.None);
        }

        /// <summary>
        /// Give L$ to an object
        /// </summary>
        /// <param name="target">object <see cref="UUID"/> to give money to</param>
        /// <param name="amount">amount of L$ to give</param>
        /// <param name="objectName">name of object</param>
        public void GiveObjectMoney(UUID target, int amount, string objectName)
        {
            GiveMoney(target, amount, objectName, MoneyTransactionType.PayObject, TransactionFlags.None);
        }

        /// <summary>
        /// Give L$ to a group
        /// </summary>
        /// <param name="target">group <see cref="UUID"/> to give money to</param>
        /// <param name="amount">amount of L$ to give</param>
        public void GiveGroupMoney(UUID target, int amount)
        {
            GiveMoney(target, amount, string.Empty, MoneyTransactionType.Gift, TransactionFlags.DestGroup);
        }

        /// <summary>
        /// Give L$ to a group
        /// </summary>
        /// <param name="target">group <see cref="UUID"/> to give money to</param>
        /// <param name="amount">amount of L$ to give</param>
        /// <param name="description">description of transaction</param>
        public void GiveGroupMoney(UUID target, int amount, string description)
        {
            GiveMoney(target, amount, description, MoneyTransactionType.Gift, TransactionFlags.DestGroup);
        }

        /// <summary>
        /// Pay texture/animation upload fee
        /// </summary>
        public void PayUploadFee()
        {
            GiveMoney(UUID.Zero, Client.Settings.UPLOAD_COST, string.Empty, MoneyTransactionType.UploadCharge,
                TransactionFlags.None);
        }

        /// <summary>
        /// Pay texture/animation upload fee
        /// </summary>
        /// <param name="description">description of the transaction</param>
        public void PayUploadFee(string description)
        {
            GiveMoney(UUID.Zero, Client.Settings.UPLOAD_COST, description, MoneyTransactionType.UploadCharge,
                TransactionFlags.None);
        }

        /// <summary>
        /// Give Money to destination Object or Avatar
        /// </summary>
        /// <param name="target">UUID of the Target Object/Avatar</param>
        /// <param name="amount">Amount in L$</param>
        /// <param name="description">Reason (Optional normally)</param>
        /// <param name="type">The type of transaction</param>
        /// <param name="flags">Transaction flags, mostly for identifying group
        /// transactions</param>
        public void GiveMoney(UUID target, int amount, string description, MoneyTransactionType type, TransactionFlags flags)
        {
            MoneyTransferRequestPacket money = new MoneyTransferRequestPacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = Client.Self.SessionID
                },
                MoneyData =
                {
                    Description = Utils.StringToBytes(description),
                    DestID = target,
                    SourceID = AgentID,
                    TransactionType = (int) type,
                    AggregatePermInventory = 0,
                    AggregatePermNextOwner = 0,
                    Flags = (byte) flags,
                    Amount = amount
                }
            };

            Client.Network.SendPacket(money);
        }

        #endregion Money
    }
}
