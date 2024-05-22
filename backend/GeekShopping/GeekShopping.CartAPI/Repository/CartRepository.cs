﻿using AutoMapper;
using GeekShopping.CartAPI.Data.ValueObjects;
using GeekShopping.CartAPI.Model;
using GeekShopping.CartAPI.Model.Context;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GeekShopping.CartAPI.Repository
{
    public class CartRepository : ICartRepoository
    {
        private readonly MySQLContext _context;
        private readonly HttpClient _httpClient;
        private readonly IMapper _mapper;

        public CartRepository(MySQLContext context,
            IMapper mapper,
            HttpClient httpClient)
        {
            _context = context;
            _mapper = mapper;
            _httpClient = httpClient;
        }

        public async Task<bool> ApplyCuopon(string userId, string couponCode)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ClearCart(string userId)
        {
            var cartHeader = await _context.CartHeaders
                        .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cartHeader != null)
            {
                _context.CartDetails
                    .RemoveRange(
                        _context.CartDetails.Where(c => c.CartHeaderId == cartHeader.Id));
                _context.CartHeaders.Remove(cartHeader);
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<CartVO> FindCartByUserId(string userId)
        {
            Cart cart = new()
            {
                CartHeader = await _context.CartHeaders
                    .FirstOrDefaultAsync(c => c.UserId == userId) ?? new CartHeader()
            };
            cart.CartDetails = _context.CartDetails
                .Where(c => c.CartHeaderId == cart.CartHeader.Id)
                .Include(p => p.Product);

            return _mapper.Map<CartVO>(cart);
        }

        public async Task<bool> RemoveCoupon(string userId)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> RemoveFromCart(long cartDetailsId)
        {
            try
            {
                CartDetail cartDetail = await _context.CartDetails
                    .FirstOrDefaultAsync(c => c.Id == cartDetailsId);
                int total = _context.CartDetails
                    .Where(c => c.CartHeaderId == cartDetail.CartHeaderId).Count();

                _context.CartDetails.Remove(cartDetail);
                if (total == 1)
                {
                    var cartHeaderToRemove = await _context.CartHeaders
                        .FirstOrDefaultAsync(c => c.Id == cartDetail.CartHeaderId);
                    _context.CartHeaders.Remove(cartHeaderToRemove);
                }

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<CartVO> SaveOrUpdateCart(CartVO vo, string token)
        {
            Cart cart = _mapper.Map<Cart>(vo);
            //valida se o produto existe salvo no banco de dados, se não existir então salve
            var product = await GetProductById(vo.CartDetails.FirstOrDefault().ProductId, token);

            if (product is null)
            {
                _context.Products.Add(cart.CartDetails.FirstOrDefault().Product);
                await _context.SaveChangesAsync();
            }

            //valida se o cabeçalho do carrinho é nulo
            var cartHeader = await _context.CartHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == cart.CartHeader.UserId);

            if (cartHeader is null)
            {
                _context.CartHeaders.Add(cart.CartHeader);
                await _context.SaveChangesAsync();
                cart.CartDetails.FirstOrDefault().CartHeaderId = cart.CartHeader.Id;
                cart.CartDetails.FirstOrDefault().Product = null;
                _context.CartDetails.Add(cart.CartDetails.FirstOrDefault());
                await _context.SaveChangesAsync();
            }
            else
            {
                var cartDetail = await _context.CartDetails.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductId == cart.CartDetails.FirstOrDefault().ProductId &&
                    p.CartHeaderId == cartHeader.Id);

                if (cartDetail is null)
                {
                    cart.CartDetails.FirstOrDefault().CartHeaderId = cartHeader.Id;
                    cart.CartDetails.FirstOrDefault().Product = null;
                    _context.CartDetails.Add(cart.CartDetails.FirstOrDefault());
                    await _context.SaveChangesAsync();
                }
                else
                {
                    cart.CartDetails.FirstOrDefault().Product = null;
                    cart.CartDetails.FirstOrDefault().Count += cartDetail.Count;
                    cart.CartDetails.FirstOrDefault().Id = cartDetail.Id;
                    cart.CartDetails.FirstOrDefault().CartHeaderId = cartDetail.CartHeaderId;
                    _context.CartDetails.Update(cart.CartDetails.FirstOrDefault());
                    await _context.SaveChangesAsync();
                }
            }

            return _mapper.Map<CartVO>(cart);
        }

        private async Task<ProductVO> GetProductById(long id, string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.GetAsync($"/api/v1/Product/{id}");
            var content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK) return new ProductVO();
            return JsonSerializer.Deserialize<ProductVO>(content,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
    }
}