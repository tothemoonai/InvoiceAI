using InvoiceAI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceAI.Data;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();
        builder.Property(i => i.IssuerName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.RegistrationNumber).HasMaxLength(20);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.ItemsJson).HasColumnType("TEXT");
        builder.Property(i => i.MissingFields).HasColumnType("TEXT");
        builder.Property(i => i.Category).HasMaxLength(50);
        builder.Property(i => i.SourceFilePath).HasMaxLength(500);
        builder.Property(i => i.FileHash).HasMaxLength(64);
        builder.Property(i => i.OcrRawText).HasColumnType("TEXT");
        builder.Property(i => i.GlmRawResponse).HasColumnType("TEXT");
        builder.HasIndex(i => i.Category);
        builder.HasIndex(i => i.TransactionDate);
        builder.HasIndex(i => i.FileHash);
    }
}