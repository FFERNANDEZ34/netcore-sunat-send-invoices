using Core.Application.Features.Invoices.Commands;
using Core.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
namespace Infrastructure.XmlServices
{
    public class UblXmlGenerator : IXmlGenerator
    {
        public string GenerateInvoiceXml(CreateInvoiceCommand command)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"");
            sb.AppendLine("         xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"");
            sb.AppendLine("         xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\"");
            sb.AppendLine("         xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\"");
            sb.AppendLine("         xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\">");

            // 🎯 1. Nodo de Extensión para la Firma Digital
            sb.AppendLine("    <ext:UBLExtensions>");
            sb.AppendLine("        <ext:UBLExtension>");
            sb.AppendLine("            <ext:ExtensionContent>");
            sb.AppendLine("                <!-- El W3cXmlSigner inyectará la firma aquí en caliente -->");
            sb.AppendLine("            </ext:ExtensionContent>");
            sb.AppendLine("        </ext:UBLExtension>");
            sb.AppendLine("    </ext:UBLExtensions>");

            // 🎯 2. Datos de Cabecera Obligatorios (UBL 2.1)
            sb.AppendLine("    <cbc:UBLVersionID>2.1</cbc:UBLVersionID>");
            sb.AppendLine("    <cbc:CustomizationID>2.0</cbc:CustomizationID>");
            sb.AppendLine($"    <cbc:ID>{command.Serie}-{command.Correlativo:D8}</cbc:ID>");
            sb.AppendLine($"    <cbc:IssueDate>{command.FechaEmision:yyyy-MM-dd}</cbc:IssueDate>");
            sb.AppendLine($"    <cbc:IssueTime>{command.FechaEmision:HH:mm:ss}</cbc:IssueTime>");
            sb.AppendLine($"    <cbc:InvoiceTypeCode listID=\"0101\">{command.TipoComprobante}</cbc:InvoiceTypeCode>");
            sb.AppendLine($"    <cbc:DocumentCurrencyCode>{command.Moneda}</cbc:DocumentCurrencyCode>");

            // 🎯 3. Firma Estructural del Documento
            sb.AppendLine("    <cac:Signature>");
            sb.AppendLine($"        <cbc:ID>S{command.Serie}-{command.Correlativo:D8}</cbc:ID>");
            sb.AppendLine("        <cac:SignatoryParty>");
            sb.AppendLine($"            <cac:PartyIdentification><cbc:ID>{command.Emisor.Ruc}</cbc:ID></cac:PartyIdentification>");
            sb.AppendLine($"            <cac:PartyName><cbc:Name><![CDATA[{command.Emisor.RazonSocial}]]></cbc:Name></cac:PartyName>");
            sb.AppendLine("        </cac:SignatoryParty>");
            sb.AppendLine("        <cac:DigitalSignatureAttachment>");
            sb.AppendLine($"            <cac:ExternalReference><cbc:URI>#Signature-{command.Emisor.Ruc}</cbc:URI></cac:ExternalReference>");
            sb.AppendLine("        </cac:DigitalSignatureAttachment>");
            sb.AppendLine("    </cac:Signature>");

            // 🎯 4. Datos del Emisor (Proveedor)
            sb.AppendLine("    <cac:AccountingSupplierParty>");
            sb.AppendLine("        <cac:Party>");
            sb.AppendLine($"            <cac:PartyIdentification><cbc:ID schemeID=\"6\">{command.Emisor.Ruc}</cbc:ID></cac:PartyIdentification>");
            sb.AppendLine("            <cac:PartyLegalEntity>");
            sb.AppendLine($"                <cbc:RegistrationName><![CDATA[{command.Emisor.RazonSocial}]]></cbc:RegistrationName>");
            sb.AppendLine("                <cac:RegistrationAddress>");
            sb.AppendLine($"                    <cbc:ID>{command.Emisor.Ubigeo}</cbc:ID>");
            sb.AppendLine("                    <cbc:AddressTypeCode>0000</cbc:AddressTypeCode>");
            sb.AppendLine($"                    <cac:AddressLine><cbc:Line><![CDATA[{command.Emisor.Direccion}]]></cbc:Line></cac:AddressLine>");
            sb.AppendLine("                    <cac:Country><cbc:IdentificationCode>PE</cbc:IdentificationCode></cac:Country>");
            sb.AppendLine("                </cac:RegistrationAddress>");
            sb.AppendLine("            </cac:PartyLegalEntity>");
            sb.AppendLine("        </cac:Party>");
            sb.AppendLine("    </cac:AccountingSupplierParty>");

            // 🎯 5. Datos del Receptor (Cliente)
            sb.AppendLine("    <cac:AccountingCustomerParty>");
            sb.AppendLine("        <cac:Party>");
            sb.AppendLine($"            <cac:PartyIdentification><cbc:ID schemeID=\"{command.Receptor.TipoDocumento}\">{command.Receptor.NumeroDocumento}</cbc:ID></cac:PartyIdentification>");
            sb.AppendLine("            <cac:PartyLegalEntity>");
            sb.AppendLine($"                <cbc:RegistrationName><![CDATA[{command.Receptor.RazonSocial}]]></cbc:RegistrationName>");
            sb.AppendLine("                </cac:PartyLegalEntity>");
            sb.AppendLine("        </cac:Party>");
            sb.AppendLine("    </cac:AccountingCustomerParty>");

            // ------------------------------------------------------------
            // 🎯 SOLUCIÓN AL ERROR 3244: Declaración Obligatoria de Forma de Pago (Contado)
            // ------------------------------------------------------------
            sb.AppendLine("    <cac:PaymentTerms>");
            sb.AppendLine("        <cbc:ID>FormaPago</cbc:ID>");
            sb.AppendLine($"        <cbc:PaymentMeansID>{command.FormaPago}</cbc:PaymentMeansID>");
            sb.AppendLine("    </cac:PaymentTerms>");

            // 🎯 6. Impuesto Global de la Factura (TaxTotal)
            sb.AppendLine("    <cac:TaxTotal>");
            sb.AppendLine($"        <cbc:TaxAmount currencyID=\"{command.Moneda}\">{command.TotalIgv.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxAmount>");
            sb.AppendLine("        <cac:TaxSubtotal>");
            sb.AppendLine($"            <cbc:TaxableAmount currencyID=\"{command.Moneda}\">{command.TotalGravado.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxableAmount>");
            sb.AppendLine($"            <cbc:TaxAmount currencyID=\"{command.Moneda}\">{command.TotalIgv.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxAmount>");
            sb.AppendLine("            <cac:TaxCategory>");
            sb.AppendLine("                <cac:TaxScheme>");
            sb.AppendLine("                    <cbc:ID>1000</cbc:ID>");
            sb.AppendLine("                    <cbc:Name>IGV</cbc:Name>");
            sb.AppendLine("                    <cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>");
            sb.AppendLine("                </cac:TaxScheme>");
            sb.AppendLine("            </cac:TaxCategory>");
            sb.AppendLine("        </cac:TaxSubtotal>");
            sb.AppendLine("    </cac:TaxTotal>");

            // 🎯 7. Totales Comerciales (LegalMonetaryTotal)
            sb.AppendLine("    <cac:LegalMonetaryTotal>");
            sb.AppendLine($"        <cbc:LineExtensionAmount currencyID=\"{command.Moneda}\">{command.TotalGravado.ToString("F2", CultureInfo.InvariantCulture)}</cbc:LineExtensionAmount>");
            sb.AppendLine($"        <cbc:TaxExclusiveAmount currencyID=\"{command.Moneda}\">{command.TotalGravado.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxExclusiveAmount>");
            sb.AppendLine($"        <cbc:TaxInclusiveAmount currencyID=\"{command.Moneda}\">{command.TotalPagar.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxInclusiveAmount>");
            sb.AppendLine($"        <cbc:PayableAmount currencyID=\"{command.Moneda}\">{command.TotalPagar.ToString("F2", CultureInfo.InvariantCulture)}</cbc:PayableAmount>");
            sb.AppendLine("    </cac:LegalMonetaryTotal>");

            // 🎯 8. Líneas de Detalle de la Factura (InvoiceLine)
            foreach (var linea in command.Lineas)
            {
                sb.AppendLine("    <cac:InvoiceLine>");
                sb.AppendLine($"        <cbc:ID>{linea.Id}</cbc:ID>");
                sb.AppendLine($"        <cbc:InvoicedQuantity unitCode=\"{linea.UnidadMedida}\">{linea.Cantidad.ToString("F2", CultureInfo.InvariantCulture)}</cbc:InvoicedQuantity>");
                sb.AppendLine($"        <cbc:LineExtensionAmount currencyID=\"{command.Moneda}\">{linea.TotalLinea.ToString("F2", CultureInfo.InvariantCulture)}</cbc:LineExtensionAmount>");

                sb.AppendLine("        <cac:PricingReference>");
                sb.AppendLine("            <cac:AlternativeConditionPrice>");
                sb.AppendLine($"                <cbc:PriceAmount currencyID=\"{command.Moneda}\">{linea.PrecioUnitario.ToString("F2", CultureInfo.InvariantCulture)}</cbc:PriceAmount>");
                sb.AppendLine("                <cbc:PriceTypeCode>01</cbc:PriceTypeCode>");
                sb.AppendLine("            </cac:AlternativeConditionPrice>");
                sb.AppendLine("        </cac:PricingReference>");

                sb.AppendLine("        <cac:TaxTotal>");
                sb.AppendLine($"            <cbc:TaxAmount currencyID=\"{command.Moneda}\">{linea.Igv.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxAmount>");
                sb.AppendLine("            <cac:TaxSubtotal>");
                sb.AppendLine($"                <cbc:TaxableAmount currencyID=\"{command.Moneda}\">{linea.TotalLinea.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxableAmount>");
                sb.AppendLine($"                <cbc:TaxAmount currencyID=\"{command.Moneda}\">{linea.Igv.ToString("F2", CultureInfo.InvariantCulture)}</cbc:TaxAmount>");
                sb.AppendLine("                <cac:TaxCategory>");
                sb.AppendLine($"                    <cbc:Percent>18.00</cbc:Percent>");
                sb.AppendLine($"                    <cbc:TaxExemptionReasonCode>{linea.TipoAfectacion}</cbc:TaxExemptionReasonCode>");
                sb.AppendLine("                    <cac:TaxScheme>");
                sb.AppendLine("                        <cbc:ID>1000</cbc:ID>");
                sb.AppendLine("                        <cbc:Name>IGV</cbc:Name>");
                sb.AppendLine("                        <cbc:TaxTypeCode>VAT</cbc:TaxTypeCode>");
                sb.AppendLine("                    </cac:TaxScheme>");
                sb.AppendLine("                </cac:TaxCategory>");
                sb.AppendLine("            </cac:TaxSubtotal>");
                sb.AppendLine("        </cac:TaxTotal>");

                sb.AppendLine("        <cac:Item>");
                sb.AppendLine($"            <cbc:Description><![CDATA[{linea.Descripcion}]]></cbc:Description>");
                sb.AppendLine("        </cac:Item>");
                sb.AppendLine("        <cac:Price>");
                sb.AppendLine($"            <cbc:PriceAmount currencyID=\"{command.Moneda}\">{linea.ValorUnitario.ToString("F4", CultureInfo.InvariantCulture)}</cbc:PriceAmount>");
                sb.AppendLine("        </cac:Price>");
                sb.AppendLine("    </cac:InvoiceLine>");
            }

            sb.AppendLine("</Invoice>");
            return sb.ToString();
        }
    }
}
