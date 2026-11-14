using Chegou.Api.Models;


namespace Chegou.Api.Domain
{
    public class EncomendaRetirada(IDbConnectionFactory connectionFactory)
    {
        public RetiradaResponse Executar(long id)
        {
 
            var result = new RetiradaResponse();

 
            return result;
        }
    }
}
