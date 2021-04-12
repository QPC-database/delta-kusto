﻿using DeltaKustoLib.CommandModel;
using DeltaKustoLib.SchemaObjects;
using Kusto.Language.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace DeltaKustoLib.KustoModel
{
    public class DatabaseModel
    {
        private static readonly IImmutableSet<Type> INPUT_COMMANDS = new[]
        {
            typeof(CreateFunctionCommand)
        }.ToImmutableHashSet();

        private IImmutableList<CreateFunctionCommand> _functionCommands;

        private DatabaseModel(
            IEnumerable<CreateFunctionCommand> functionCommands)
        {
            _functionCommands = functionCommands.ToImmutableArray();
        }

        public static DatabaseModel FromCommands(
            IEnumerable<CommandBase> commands)
        {
            ValidateCommandTypes(commands.Select(c => (c.GetType(), c.ObjectFriendlyTypeName)).Distinct());

            var functions = commands
                .OfType<CreateFunctionCommand>()
                .ToImmutableArray();

            ValidateDuplicates("Functions", functions);

            return new DatabaseModel(functions);
        }

        public static DatabaseModel FromDatabaseSchema(DatabaseSchema databaseSchema)
        {
            var functions = databaseSchema
                .Functions
                .Values
                .Select(s => FromFunctionSchema(s));

            return new DatabaseModel(functions);
        }

        public IImmutableList<CommandBase> ComputeDelta(DatabaseModel targetModel)
        {
            var functions =
                CreateFunctionCommand.ComputeDelta(_functionCommands, targetModel._functionCommands);
            var deltaCommands = functions;

            return deltaCommands.ToImmutableArray();
        }

        #region Functions
        private static CreateFunctionCommand FromFunctionSchema(FunctionSchema schema)
        {
            var parameters = schema
                .InputParameters
                .Select(i => FromParameterSchema(i));
            var body = TrimFunctionSchemaBody(schema.Body);

            return new CreateFunctionCommand(
                schema.Name,
                parameters,
                body,
                schema.Folder,
                schema.DocString,
                true);
        }

        private static string TrimFunctionSchemaBody(string body)
        {
            var trimmedBody = body.Trim();

            if (trimmedBody.Length < 2)
            {
                throw new InvalidOperationException(
                    $"Function body should at least be 2 characters but isn't:  {body}");
            }
            if (trimmedBody.First() != '{' || trimmedBody.Last() != '}')
            {
                throw new InvalidOperationException(
                    $"Function body was expected to be surrounded by curly brace but isn't:"
                    + $"  {body}");
            }

            var actualBody = trimmedBody
                .Substring(1, trimmedBody.Length - 2)
                //  This trim removes the carriage return so they don't accumulate in translations
                .Trim();

            return actualBody;
        }
        #endregion

        private static TypedParameterModel FromParameterSchema(InputParameterSchema input)
        {
            return input.CslType == null
                ? new TypedParameterModel(
                    input.Name,
                    new TableParameterModel(input.Columns.Select(c => new ColumnModel(c.Name, c.CslType))))
                : new TypedParameterModel(
                    input.Name,
                    input.CslType,
                    input.CslDefaultValue != null ? "=" + input.CslDefaultValue : null);
        }

        private static void ValidateCommandTypes(IEnumerable<(Type type, string friendlyName)> commandTypes)
        {
            var extraCommandTypes = commandTypes
                .Select(p => p.type)
                .Except(INPUT_COMMANDS);

            if (extraCommandTypes.Any())
            {
                var typeToNameMap = commandTypes
                    .ToImmutableDictionary(p => p.type, p => p.friendlyName);

                throw new DeltaException(
                    "Unsupported command types:  "
                    + $"{string.Join(", ", extraCommandTypes.Select(t => typeToNameMap[t]))}");
            }
        }

        private static void ValidateDuplicates<T>(
            string friendlyObjectType,
            IEnumerable<T> dbObjects)
            where T : CommandBase
        {
            var functionDuplicates = dbObjects
                .GroupBy(o => o.ObjectName)
                .Where(g => g.Count() > 1)
                .Select(g => new { Name = g.Key, Objects = g.ToArray(), Count = g.Count() });

            if (functionDuplicates.Any())
            {
                var duplicateText = string.Join(
                    ", ",
                    functionDuplicates.Select(d => $"(Name = '{d.Name}', Count = {d.Count})"));

                throw new DeltaException(
                    $"{friendlyObjectType} have duplicates:  {{ {duplicateText} }}");
            }
        }
    }
}