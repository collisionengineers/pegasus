using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // ENG-014: manifest.sha256 and provenance.json were never an operator
    // requirement -- they entered the governing docs through internal doc
    // restructuring, and the word "manifest" appears nowhere in the whole
    // operator-supplied reference corpus. In the database they were pure
    // write-only ballast: persisted here, read straight back into a
    // reconstructed EvaBundle, and never consumed by any importer,
    // verifier, page or script. The archive now carries the ordered
    // thirteen-key JSON and Images/ only, so nothing produces them.
    //
    // Not additive, unlike the usual release rule: an application built
    // before ENG-014 lists these three columns in its EvaHandoffRevisions
    // insert, so rolling the app back behind this migration fails EVA
    // hand-off GENERATION until it is rolled forward again. Nothing is
    // lost and nothing else is touched -- existing revisions keep their
    // bundle, JSON and hashes, and download serves BundleContent as
    // before. Down() restores the columns (empty), not their old content.
    public partial class DropEvaHandoffProvenanceAndManifest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManifestContent",
                table: "EvaHandoffRevisions");

            migrationBuilder.DropColumn(
                name: "ProvenanceContent",
                table: "EvaHandoffRevisions");

            migrationBuilder.DropColumn(
                name: "ProvenanceSha256",
                table: "EvaHandoffRevisions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ManifestContent",
                table: "EvaHandoffRevisions",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<byte[]>(
                name: "ProvenanceContent",
                table: "EvaHandoffRevisions",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<string>(
                name: "ProvenanceSha256",
                table: "EvaHandoffRevisions",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}
